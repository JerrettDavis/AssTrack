using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class DeviceSummaryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DeviceSummaryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSummary_ReturnsNotFound_ForUnknownDevice()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/devices/{Guid.NewGuid()}/summary");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSummary_ReturnsZeroAlerts_ForDeviceWithNoAlerts()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-summary-001" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
            db.Observations.Add(new Observation
            {
                DeviceId = deviceId,
                ObservedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 10.0,
                Longitude = 20.0,
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/devices/{deviceId}/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeviceSummaryDto>();
        dto.Should().NotBeNull();
        dto!.UnacknowledgedSpeedAlerts.Should().Be(0);
        dto.UnacknowledgedGeofenceBreaches.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_ReturnsLatestObservationData_WhenObservationExists()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        var observedAt = DateTime.UtcNow.AddMinutes(-1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-summary-002" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
            db.Observations.Add(new Observation
            {
                DeviceId = deviceId,
                ObservedAt = observedAt,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 51.5074,
                Longitude = -0.1278,
                SpeedKmh = 42.5,
                HeadingDegrees = 180.0,
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/devices/{deviceId}/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeviceSummaryDto>();
        dto.Should().NotBeNull();
        dto!.LastLatitude.Should().BeApproximately(51.5074, 0.0001);
        dto.LastLongitude.Should().BeApproximately(-0.1278, 0.0001);
        dto.LatestSpeedKmh.Should().BeApproximately(42.5, 0.01);
        dto.LatestHeadingDegrees.Should().BeApproximately(180.0, 0.01);
        dto.LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSummary_CountsUnacknowledgedAlerts_ForDevice()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-summary-003" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;

            var observation = new Observation
            {
                DeviceId = deviceId,
                ObservedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 0,
                Longitude = 0,
                SpeedKmh = 120
            };
            db.Observations.Add(observation);
            await db.SaveChangesAsync();

            db.SpeedAlerts.Add(new SpeedAlert { DeviceId = deviceId, ObservationId = observation.Id, TriggeredAt = DateTime.UtcNow, ObservedSpeedKmh = 120, ThresholdKmh = 100 });
            db.SpeedAlerts.Add(new SpeedAlert { DeviceId = deviceId, ObservationId = observation.Id, TriggeredAt = DateTime.UtcNow, ObservedSpeedKmh = 130, ThresholdKmh = 100 });
            db.SpeedAlerts.Add(new SpeedAlert { DeviceId = deviceId, ObservationId = observation.Id, TriggeredAt = DateTime.UtcNow, ObservedSpeedKmh = 110, ThresholdKmh = 100, AcknowledgedAtUtc = DateTime.UtcNow });

            var geofence = new Geofence { Name = "TestFence", CenterLatitude = 0, CenterLongitude = 0, RadiusMeters = 100 };
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            db.GeofenceBreaches.Add(new GeofenceBreach { DeviceId = deviceId, ObservationId = observation.Id, GeofenceId = geofence.Id, DetectedAt = DateTime.UtcNow, EventType = GeofenceBreachEventType.Enter });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/devices/{deviceId}/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeviceSummaryDto>();
        dto.Should().NotBeNull();
        dto!.UnacknowledgedSpeedAlerts.Should().Be(2);
        dto.UnacknowledgedGeofenceBreaches.Should().Be(1);
    }
}
