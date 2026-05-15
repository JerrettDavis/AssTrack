using System.Net;
using System.Net.Http.Json;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssTrack.Tests.Api;

/// <summary>
/// Test factory that keeps the real WebhookNotificationService (with delivery log
/// persistence) but replaces the outbound HTTP handler with a controllable fake.
/// </summary>
public sealed class WebhookDeliveryLogFactory : TestWebApplicationFactory
{
    public CapturingHttpMessageHandler WebhookHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);  // sets up in-memory SQLite + NullWebhookService

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhooks:Url"] = "https://hooks.test.local/delivery",
                ["Webhooks:MaxRetries"] = "0"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the NullWebhookService added by base with the real one + capturing handler.
            services.RemoveAll<IWebhookNotificationService>();
            services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>()
                .ConfigurePrimaryHttpMessageHandler(() => WebhookHandler);
        });
    }
}

public class WebhookDeliveryLogTests : IClassFixture<WebhookDeliveryLogFactory>
{
    private readonly WebhookDeliveryLogFactory _factory;

    public WebhookDeliveryLogTests(WebhookDeliveryLogFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedDeviceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var device = new Device { Identifier = $"DL-DEV-{Guid.NewGuid():N}" };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }

    private async Task IngestSpeedingObservationAsync(HttpClient client, Guid deviceId)
    {
        var request = new CreateObservationRequest(
            deviceId, DateTime.UtcNow, 51.5, -0.1, null, null, 150, null, null);
        await client.PostAsJsonAsync("/api/observations", request);
    }

    [Fact]
    public async Task DeliveryLog_IsCreated_OnSuccessfulWebhook()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        _factory.WebhookHandler.ShouldThrow = false;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await IngestSpeedingObservationAsync(client, deviceId);

        // Allow async log persistence to finish
        await Task.Delay(200);

        var response = await client.GetAsync("/api/webhooks/deliveries");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(l => l.EventType == "speed_alert" && l.Success);
    }

    [Fact]
    public async Task DeliveryLog_IsCreated_OnHttpFailureAndExceptionIsSwallowed()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        _factory.WebhookHandler.ShouldThrow = false;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // Must not throw even though webhook returns 500
        var act = async () => await IngestSpeedingObservationAsync(client, deviceId);
        await act.Should().NotThrowAsync();

        await Task.Delay(200);

        var response = await client.GetAsync("/api/webhooks/deliveries");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        result!.Items.Should().ContainSingle(l =>
            l.EventType == "speed_alert" &&
            !l.Success &&
            l.HttpStatusCode == 500 &&
            l.AttemptNumber == 1);
    }

    [Fact]
    public async Task FailedDelivery_CanBeReplayed_ToOriginalTarget()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.Reset();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await IngestSpeedingObservationAsync(client, deviceId);
        await Task.Delay(200);

        var failedResponse = await client.GetAsync("/api/webhooks/deliveries?success=false");
        var failedResult = await failedResponse.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        var failed = failedResult!.Items.Single();

        _factory.WebhookHandler.Reset();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        var replayResponse = await client.PostAsync($"/api/webhooks/deliveries/{failed.Id}/replay", null);

        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await replayResponse.Content.ReadFromJsonAsync<WebhookReplayResponse>();
        replay.Should().NotBeNull();
        replay!.Replayed.Should().BeTrue();
        replay.TargetUrl.Should().Be("https://hooks.test.local/delivery");
        _factory.WebhookHandler.LastRequest.Should().NotBeNull();
        _factory.WebhookHandler.LastRequest!.RequestUri!.ToString().Should().Be("https://hooks.test.local/delivery");
        _factory.WebhookHandler.LastRequestBody.Should().Contain("speed_alert");

        var deliveries = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=speed_alert");
        deliveries!.Items.Should().Contain(x => x.AttemptNumber == failed.AttemptNumber + 1 && x.Success);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?action=webhook_delivery.replayed");
        audit.Should().NotBeNull();
        audit!.Items.Should().ContainSingle(x => x.EntityId == failed.Id.ToString() && x.EntityType == "webhook_delivery");
    }

    [Fact]
    public async Task Replay_ReturnsConflict_WhenFullPayloadWasNotRetained()
    {
        await _factory.ResetDatabaseAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            db.WebhookDeliveryLogs.Add(new WebhookDeliveryLog
            {
                AttemptedAt = DateTime.UtcNow,
                EventType = "speed_alert",
                TargetUrl = "https://hooks.test.local/delivery",
                Success = false,
                AttemptNumber = 1,
                CorrelationId = Guid.NewGuid().ToString(),
                RequestPayloadSummary = """{"eventType":"speed_alert"}"""
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries");
        var response = await client.PostAsync($"/api/webhooks/deliveries/{logs!.Items.Single().Id}/replay", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeliveryLog_IsCreated_OnNetworkFailureAndExceptionIsSwallowed()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ShouldThrow = true;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // Ingest must not throw despite the network failure
        var act = async () => await IngestSpeedingObservationAsync(client, deviceId);
        await act.Should().NotThrowAsync();

        await Task.Delay(200);

        _factory.WebhookHandler.ShouldThrow = false;  // reset for next test

        var response = await client.GetAsync("/api/webhooks/deliveries");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        result!.Items.Should().ContainSingle(l => l.EventType == "speed_alert" && !l.Success && l.AttemptNumber == 1);
    }

    [Fact]
    public async Task Deliveries_Filter_BySuccess_ReturnsOnlyMatchingRows()
    {
        await _factory.ResetDatabaseAsync();

        // Create one success log and one failure log
        _factory.WebhookHandler.ShouldThrow = false;
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        var deviceId1 = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await IngestSpeedingObservationAsync(client, deviceId1);
        await Task.Delay(100);

        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        var deviceId2 = await SeedDeviceAsync();
        await IngestSpeedingObservationAsync(client, deviceId2);
        await Task.Delay(100);
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;  // reset

        // Filter success=true
        var successResp = await client.GetAsync("/api/webhooks/deliveries?success=true");
        var successResult = await successResp.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        successResult!.Items.Should().AllSatisfy(l => l.Success.Should().BeTrue());

        // Filter success=false
        var failResp = await client.GetAsync("/api/webhooks/deliveries?success=false");
        var failResult = await failResp.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        failResult!.Items.Should().AllSatisfy(l => l.Success.Should().BeFalse());
    }

    [Fact]
    public async Task Deliveries_Filter_ByEventType_ReturnsOnlyMatchingRows()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ShouldThrow = false;
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await IngestSpeedingObservationAsync(client, deviceId);
        await Task.Delay(200);

        var resp = await client.GetAsync("/api/webhooks/deliveries?eventType=speed_alert");
        var result = await resp.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(l => l.EventType.Should().Be("speed_alert"));

        var emptyResp = await client.GetAsync("/api/webhooks/deliveries?eventType=nonexistent");
        var emptyResult = await emptyResp.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        emptyResult!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Status_Returns_ConfiguredTrue_And_CorrectCounts()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ShouldThrow = false;
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;

        var deviceId = await SeedDeviceAsync();
        using var client = _factory.CreateAuthenticatedClient();
        await IngestSpeedingObservationAsync(client, deviceId);
        await Task.Delay(200);

        var resp = await client.GetAsync("/api/webhooks/status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await resp.Content.ReadFromJsonAsync<WebhookStatusDto>();
        status.Should().NotBeNull();
        status!.Configured.Should().BeTrue();
        status.Last24hDeliveries.Should().BeGreaterThanOrEqualTo(1);
        status.LastDeliveredAt.Should().NotBeNull();
        status.AvgDurationMs.Should().NotBeNull();
    }

    [Fact]
    public async Task Status_Returns_ConfiguredFalse_WhenUrlNotSet()
    {
        // Use the default TestWebApplicationFactory (NullWebhookService, no URL configured)
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();

        var resp = await client.GetAsync("/api/webhooks/status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await resp.Content.ReadFromJsonAsync<WebhookStatusDto>();
        status!.Configured.Should().BeFalse();
        status.Last24hDeliveries.Should().Be(0);
    }
}
