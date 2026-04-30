using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class WebhookTestFireTests_Configured : IClassFixture<WebhookDeliveryLogFactory>
{
    private readonly WebhookDeliveryLogFactory _factory;

    public WebhookTestFireTests_Configured(WebhookDeliveryLogFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestFire_SpeedAlert_WhenConfigured_ReturnsConfiguredTrue_AndCreatesDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        _factory.WebhookHandler.ShouldThrow = false;

        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/webhooks/test", new { eventType = "speed_alert" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TestWebhookFireResponse>();
        result.Should().NotBeNull();
        result!.Fired.Should().BeTrue();
        result.Configured.Should().BeTrue();
        result.EventType.Should().Be("speed_alert");
        result.Message.Should().Contain("Check delivery logs");

        await Task.Delay(200);

        var logResponse = await client.GetAsync("/api/webhooks/deliveries");
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logResult = await logResponse.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        logResult.Should().NotBeNull();
        logResult!.Items.Should().ContainSingle(x => x.EventType == "speed_alert");
    }

    [Fact]
    public async Task TestFire_GeofenceBreach_WhenConfigured_ReturnsConfiguredTrue_AndCreatesDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        _factory.WebhookHandler.ShouldThrow = false;

        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/webhooks/test", new { eventType = "geofence_breach" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TestWebhookFireResponse>();
        result.Should().NotBeNull();
        result!.Fired.Should().BeTrue();
        result.Configured.Should().BeTrue();
        result.EventType.Should().Be("geofence_breach");
        result.Message.Should().Contain("Check delivery logs");

        await Task.Delay(200);

        var logResponse = await client.GetAsync("/api/webhooks/deliveries");
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logResult = await logResponse.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        logResult.Should().NotBeNull();
        logResult!.Items.Should().ContainSingle(x => x.EventType == "geofence_breach");
    }

    [Fact]
    public async Task TestFire_DefaultsToSpeedAlert_WhenBodyIsEmpty()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        _factory.WebhookHandler.ShouldThrow = false;

        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/webhooks/test", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TestWebhookFireResponse>();
        result.Should().NotBeNull();
        result!.EventType.Should().Be("speed_alert");
        result.Configured.Should().BeTrue();
    }
}

public class WebhookTestFireTests_NotConfigured : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WebhookTestFireTests_NotConfigured(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestFire_WhenNotConfigured_ReturnsConfiguredFalse_NoDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();

        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/webhooks/test", new { eventType = "speed_alert" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TestWebhookFireResponse>();
        result.Should().NotBeNull();
        result!.Fired.Should().BeTrue();
        result.Configured.Should().BeFalse();
        result.Message.Should().Contain("No webhook URL configured");

        await Task.Delay(200);

        var logResponse = await client.GetAsync("/api/webhooks/deliveries");
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logResult = await logResponse.Content.ReadFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>();
        logResult.Should().NotBeNull();
        logResult!.Items.Should().BeEmpty();
    }
}
