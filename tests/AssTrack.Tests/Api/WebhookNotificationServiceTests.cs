using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssTrack.Tests.Api;

/// <summary>
/// Captures the last HTTP request sent through it for assertion.
/// </summary>
public sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public bool ShouldThrow { get; set; }

    public void Reset()
    {
        LastRequest = null;
        LastRequestBody = null;
        ResponseStatusCode = HttpStatusCode.OK;
        ShouldThrow = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ShouldThrow)
            throw new HttpRequestException("Simulated network failure");

        LastRequest = request;
        LastRequestBody = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        return new HttpResponseMessage(ResponseStatusCode);
    }
}

public class WebhookNotificationServiceTests
{
    private static (WebhookNotificationService Service, CapturingHttpMessageHandler Handler)
        Build(string? url = "https://hooks.example.com/asstrack", int timeoutSeconds = 5)
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new WebhookOptions { Url = url, TimeoutSeconds = timeoutSeconds });
        var logger = NullLogger<WebhookNotificationService>.Instance;
        var service = new WebhookNotificationService(httpClient, options, logger);
        return (service, handler);
    }

    private static SpeedAlert MakeSpeedAlert() => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        AssetId = Guid.NewGuid(),
        ObservedSpeedKmh = 145.5,
        ThresholdKmh = 120.0,
        TriggeredAt = DateTime.UtcNow,
        Device = new Device { Identifier = "DEV-001" },
        Asset = new Asset { Name = "Fleet Van 1" }
    };

    private static GeofenceBreach MakeBreach(GeofenceBreachEventType eventType = GeofenceBreachEventType.Enter)
        => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            GeofenceId = Guid.NewGuid(),
            DetectedAt = DateTime.UtcNow,
            EventType = eventType,
            Device = new Device { Identifier = "DEV-002" },
            Asset = new Asset { Name = "Generator A" },
            Geofence = new Geofence { Name = "Site Alpha", CenterLatitude = 51.5, CenterLongitude = -0.1, RadiusMeters = 500 }
        };

    [Fact]
    public async Task NotifySpeedAlertAsync_PostsToConfiguredUrl()
    {
        var (service, handler) = Build();
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://hooks.example.com/asstrack");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task NotifySpeedAlertAsync_PayloadContainsCorrectFields()
    {
        var (service, handler) = Build();
        var alert = MakeSpeedAlert();
        await service.NotifySpeedAlertAsync(alert);

        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;

        root.GetProperty("eventType").GetString().Should().Be("speed_alert");
        root.GetProperty("alertId").GetGuid().Should().Be(alert.Id);
        root.GetProperty("deviceId").GetGuid().Should().Be(alert.DeviceId);
        root.GetProperty("deviceIdentifier").GetString().Should().Be("DEV-001");
        root.GetProperty("assetId").GetGuid().Should().Be(alert.AssetId!.Value);
        root.GetProperty("assetName").GetString().Should().Be("Fleet Van 1");
        root.GetProperty("observedSpeedKmh").GetDouble().Should().Be(145.5);
        root.GetProperty("thresholdKmh").GetDouble().Should().Be(120.0);
        root.GetProperty("deliveredAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NotifySpeedAlertAsync_DoesNotPost_WhenUrlIsEmpty()
    {
        var (service, handler) = Build(url: "");
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task NotifySpeedAlertAsync_DoesNotPost_WhenUrlIsNull()
    {
        var (service, handler) = Build(url: null);
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task NotifySpeedAlertAsync_LogsAndContinues_OnNonSuccessResponse()
    {
        var (service, handler) = Build();
        handler.ResponseStatusCode = HttpStatusCode.InternalServerError;

        // Should not throw even on 500
        var act = async () => await service.NotifySpeedAlertAsync(MakeSpeedAlert());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifySpeedAlertAsync_LogsAndContinues_OnNetworkFailure()
    {
        var (service, handler) = Build();
        handler.ShouldThrow = true;

        var act = async () => await service.NotifySpeedAlertAsync(MakeSpeedAlert());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyGeofenceBreachAsync_PostsToConfiguredUrl()
    {
        var (service, handler) = Build();
        await service.NotifyGeofenceBreachAsync(MakeBreach());

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task NotifyGeofenceBreachAsync_PayloadContainsCorrectFields()
    {
        var (service, handler) = Build();
        var breach = MakeBreach(GeofenceBreachEventType.Exit);
        await service.NotifyGeofenceBreachAsync(breach);

        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;

        root.GetProperty("eventType").GetString().Should().Be("geofence_breach");
        root.GetProperty("breachId").GetGuid().Should().Be(breach.Id);
        root.GetProperty("deviceId").GetGuid().Should().Be(breach.DeviceId);
        root.GetProperty("deviceIdentifier").GetString().Should().Be("DEV-002");
        root.GetProperty("assetName").GetString().Should().Be("Generator A");
        root.GetProperty("geofenceName").GetString().Should().Be("Site Alpha");
        root.GetProperty("breachEventType").GetString().Should().Be("Exit");
        root.GetProperty("deliveredAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NotifyGeofenceBreachAsync_DoesNotPost_WhenUrlIsEmpty()
    {
        var (service, handler) = Build(url: "");
        await service.NotifyGeofenceBreachAsync(MakeBreach());

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task NotifyGeofenceBreachAsync_LogsAndContinues_OnNetworkFailure()
    {
        var (service, handler) = Build();
        handler.ShouldThrow = true;

        var act = async () => await service.NotifyGeofenceBreachAsync(MakeBreach());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyGeofenceBreachAsync_EnterEvent_PayloadHasCorrectBreachEventType()
    {
        var (service, handler) = Build();
        var breach = MakeBreach(GeofenceBreachEventType.Enter);
        await service.NotifyGeofenceBreachAsync(breach);

        var doc = JsonDocument.Parse(handler.LastRequestBody!);
        doc.RootElement.GetProperty("breachEventType").GetString().Should().Be("Enter");
    }

    // Helper overload for signing tests
    private static (WebhookNotificationService Service, CapturingHttpMessageHandler Handler)
        BuildWithSigning(string signingSecret, string? url = "https://hooks.example.com/asstrack")
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new WebhookOptions { Url = url, TimeoutSeconds = 5, SigningSecret = signingSecret });
        var logger = NullLogger<WebhookNotificationService>.Instance;
        var service = new WebhookNotificationService(httpClient, options, logger);
        return (service, handler);
    }

    [Fact]
    public async Task SigningEnabled_AddsSignatureHeader()
    {
        var (service, handler) = BuildWithSigning("test-secret");
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.TryGetValues("X-AssTrack-Signature-256", out var values).Should().BeTrue();
        var sig = values!.First();
        sig.Should().StartWith("sha256=");
        sig.Length.Should().BeGreaterThan(7); // "sha256=" + 64 hex chars
    }

    [Fact]
    public async Task SigningEnabled_SignatureMatchesHmacOfBody()
    {
        var secret = "my-signing-secret";
        var (service, handler) = BuildWithSigning(secret);
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        var body = handler.LastRequestBody!;
        var sig = handler.LastRequest!.Headers.GetValues("X-AssTrack-Signature-256").First();

        // Recompute HMAC-SHA256
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(bodyBytes);
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        sig.Should().Be(expected);
    }

    [Fact]
    public async Task SigningDisabled_NoSignatureHeader()
    {
        var (service, handler) = Build(); // no signing secret
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Contains("X-AssTrack-Signature-256").Should().BeFalse();
    }

    [Fact]
    public async Task SigningEnabled_EmptySecret_NoSignatureHeader()
    {
        var (service, handler) = BuildWithSigning(""); // empty = disabled
        await service.NotifySpeedAlertAsync(MakeSpeedAlert());

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Contains("X-AssTrack-Signature-256").Should().BeFalse();
    }
}
