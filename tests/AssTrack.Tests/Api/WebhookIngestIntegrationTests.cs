using System.Net;
using System.Net.Http.Json;
using AssTrack.Api.Services;
using AssTrack.Domain.Models;
using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssTrack.Tests.Api;

/// <summary>
/// Fake webhook service that records calls so integration tests can assert on them.
/// </summary>
public sealed class RecordingWebhookService : IWebhookNotificationService
{
    public List<SpeedAlert> SpeedAlerts { get; } = [];
    public List<GeofenceBreach> Breaches { get; } = [];
    public List<IntegrationEvent> IntegrationEvents { get; } = [];

    public Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default)
    {
        SpeedAlerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
    {
        Breaches.Add(breach);
        return Task.CompletedTask;
    }

    public Task NotifyIntegrationEventAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        IntegrationEvents.Add(integrationEvent);
        return Task.CompletedTask;
    }

    public Task ExecuteRetryAsync(WebhookRetryJob job, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class WebhookCapturingFactory : TestWebApplicationFactory
{
    public RecordingWebhookService WebhookService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWebhookNotificationService>();
            services.AddSingleton<IWebhookNotificationService>(WebhookService);
        });
    }
}

public class WebhookIngestIntegrationTests : IClassFixture<WebhookCapturingFactory>
{
    private readonly WebhookCapturingFactory _factory;

    public WebhookIngestIntegrationTests(WebhookCapturingFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ingest_AboveSpeedThreshold_TriggersWebhookNotification()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookService.SpeedAlerts.Clear();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Test Van", Category = "Vehicle" };
            var device = new Device { Identifier = "WH-DEV-001", Asset = asset };
            db.Assets.Add(asset);
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5, -0.1, null, null, 150, null, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.WebhookService.SpeedAlerts.Should().ContainSingle();
        _factory.WebhookService.SpeedAlerts[0].ObservedSpeedKmh.Should().Be(150);
        _factory.WebhookService.SpeedAlerts[0].Device.Should().NotBeNull();
        _factory.WebhookService.SpeedAlerts[0].Device!.Identifier.Should().Be("WH-DEV-001");
        _factory.WebhookService.SpeedAlerts[0].Asset.Should().NotBeNull();
        _factory.WebhookService.SpeedAlerts[0].Asset!.Name.Should().Be("Test Van");
    }

    [Fact]
    public async Task Ingest_BelowSpeedThreshold_DoesNotTriggerSpeedAlertWebhook()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookService.SpeedAlerts.Clear();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "WH-DEV-002" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5, -0.1, null, null, 80, null, null);
        await client.PostAsJsonAsync("/api/observations", request);

        _factory.WebhookService.SpeedAlerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Ingest_InsideGeofence_TriggersBreachWebhook()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookService.Breaches.Clear();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Pump Unit", Category = "Equipment" };
            var device = new Device { Identifier = "WH-DEV-003", Asset = asset };
            var geofence = new Geofence
            {
                Name = "Depot Zone",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 5000,
                IsActive = true
            };
            db.Assets.Add(asset);
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        // Observation inside the geofence
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 60, null, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.WebhookService.Breaches.Should().ContainSingle();
        _factory.WebhookService.Breaches[0].EventType.Should().Be(GeofenceBreachEventType.Enter);
        _factory.WebhookService.Breaches[0].Geofence.Should().NotBeNull();
        _factory.WebhookService.Breaches[0].Geofence!.Name.Should().Be("Depot Zone");
        _factory.WebhookService.Breaches[0].Device.Should().NotBeNull();
        _factory.WebhookService.Breaches[0].Device!.Identifier.Should().Be("WH-DEV-003");
    }

    [Fact]
    public async Task Ingest_ExitGeofence_TriggersExitBreachWebhook()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookService.Breaches.Clear();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "WH-DEV-004" };
            var geofence = new Geofence
            {
                Name = "Exit Zone",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 5000,
                IsActive = true
            };
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();

        // First: enter the geofence
        var enterRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(-10), 51.5074, -0.1278, null, null, 50, null, null);
        await client.PostAsJsonAsync("/api/observations", enterRequest);
        _factory.WebhookService.Breaches.Clear();

        // Then: exit the geofence
        var exitRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow, 40.7128, -74.0060, null, null, 50, null, null);
        var exitResponse = await client.PostAsJsonAsync("/api/observations", exitRequest);

        exitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.WebhookService.Breaches.Should().ContainSingle();
        _factory.WebhookService.Breaches[0].EventType.Should().Be(GeofenceBreachEventType.Exit);
    }

    [Fact]
    public async Task Ingest_WithinCooldown_DoesNotTriggerSecondSpeedAlertWebhook()
    {
        await _factory.ResetDatabaseAsync();
        _factory.WebhookService.SpeedAlerts.Clear();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "WH-DEV-005" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var req1 = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5, -0.1, null, null, 130, null, null);
        await client.PostAsJsonAsync("/api/observations", req1);

        var req2 = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(-1), 51.5, -0.1, null, null, 135, null, null);
        await client.PostAsJsonAsync("/api/observations", req2);

        _factory.WebhookService.SpeedAlerts.Should().ContainSingle("second alert within cooldown should be suppressed");
    }
}
