using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssTrack.Tests.Api;

public class GeofenceBreachTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public GeofenceBreachTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostObservationInsideGeofence_Creates_GeofenceBreach()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "GeoAsset", Category = "Equipment" };
            var device = new Device { Identifier = "geo-dev-001", Asset = asset };
            var geofence = new Geofence
            {
                Name = "Test Zone",
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
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 50, null, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verifyDb.GeofenceBreaches.ToListAsync();
        breaches.Should().ContainSingle();
        breaches[0].DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task PostObservationOutsideGeofence_Does_Not_CreateBreach()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-dev-002" };
            var geofence = new Geofence
            {
                Name = "Small Zone",
                CenterLatitude = 40.7128,
                CenterLongitude = -74.0060,
                RadiusMeters = 10,
                IsActive = true
            };
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        // Post observation far from geofence center
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 50, null, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verifyDb.GeofenceBreaches.ToListAsync();
        breaches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGeofenceBreaches_ReturnsBreaches()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-dev-003" };
            var geofence = new Geofence
            {
                Name = "London Zone",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 10000,
                IsActive = true
            };
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 60, null, null);
        await client.PostAsJsonAsync("/api/observations", request);

        var breaches = await client.GetFromJsonAsync<List<GeofenceBreachDto>>("/api/geofences/breaches");
        breaches.Should().NotBeNull();
        breaches!.Should().ContainSingle();
        breaches[0].GeofenceName.Should().Be("London Zone");
    }
}
