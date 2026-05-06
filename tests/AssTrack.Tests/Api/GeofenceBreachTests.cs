using System.Linq;
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
    public async Task PostObservationInsideGeofence_Twice_CreatesSingleBreach()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-dev-004" };
            var geofence = new Geofence
            {
                Name = "Dedup Zone",
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
        var request1 = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 50, null, null);
        await client.PostAsJsonAsync("/api/observations", request1);

        var request2 = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(1), 51.5074, -0.1278, null, null, 50, null, null);
        await client.PostAsJsonAsync("/api/observations", request2);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verifyDb.GeofenceBreaches.ToListAsync();
        breaches.Should().ContainSingle("second observation inside same geofence should not create duplicate breach");
    }

    [Fact]
    public async Task PostObservationExitsGeofence_Creates_ExitBreach()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-dev-005" };
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
        // Enter the geofence
        var enterRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 50, null, null);
        await client.PostAsJsonAsync("/api/observations", enterRequest);

        // Exit the geofence (far away from center)
        var exitRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(1), 40.7128, -74.0060, null, null, 50, null, null);
        await client.PostAsJsonAsync("/api/observations", exitRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verifyDb.GeofenceBreaches.OrderBy(b => b.DetectedAt).ToListAsync();
        breaches.Should().HaveCount(2);
        breaches[0].EventType.Should().Be(GeofenceBreachEventType.Enter);
        breaches[1].EventType.Should().Be(GeofenceBreachEventType.Exit);
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

    [Fact]
    public async Task GetGeofenceBreaches_WithPagination_ReturnsPagedResult()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-dev-pag" };
            var geofence = new Geofence
            {
                Name = "Pagination Zone",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 10000,
                IsActive = true
            };
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;

            // Insert 5 observations and their breaches directly to bypass enter/exit state tracking
            for (int i = 0; i < 5; i++)
            {
                var observation = new Observation
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    ObservedAt = DateTime.UtcNow.AddMinutes(-i - 10),
                    ReceivedAt = DateTime.UtcNow.AddMinutes(-i - 10),
                    Latitude = 51.5074,
                    Longitude = -0.1278
                };
                db.Observations.Add(observation);
                db.GeofenceBreaches.Add(new GeofenceBreach
                {
                    Id = Guid.NewGuid(),
                    ObservationId = observation.Id,
                    DeviceId = device.Id,
                    GeofenceId = geofence.Id,
                    EventType = i % 2 == 0 ? GeofenceBreachEventType.Enter : GeofenceBreachEventType.Exit,
                    DetectedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();

        var page1Response = await client.GetAsync("/api/geofences/breaches?page=1&pageSize=2");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var page1 = await page1Response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        page1.GetProperty("items").GetArrayLength().Should().Be(2);
        page1.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(5);
        page1.GetProperty("page").GetInt32().Should().Be(1);
        page1.GetProperty("pageSize").GetInt32().Should().Be(2);

        var page2Response = await client.GetAsync("/api/geofences/breaches?page=2&pageSize=2");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var page2 = await page2Response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        page2.GetProperty("items").GetArrayLength().Should().Be(2);
        page2.GetProperty("page").GetInt32().Should().Be(2);
        page2.GetProperty("pageSize").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task PostObservationInsidePolygonGeofence_Creates_GeofenceBreach()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "geo-poly-001" };
            var geofence = new Geofence
            {
                Name = "Polygon Zone",
                ShapeType = "polygon",
                CenterLatitude = 36.1,
                CenterLongitude = -95.85,
                RadiusMeters = 0,
                PolygonJson = """
                    [
                      { "latitude": 36.0, "longitude": -96.0 },
                      { "latitude": 36.2, "longitude": -96.0 },
                      { "latitude": 36.2, "longitude": -95.7 },
                      { "latitude": 36.0, "longitude": -95.7 }
                    ]
                    """,
                IsActive = true
            };
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 36.05, -95.9, null, null, 50, null, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verifyDb.GeofenceBreaches.ToListAsync();
        breaches.Should().ContainSingle();
        breaches[0].DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task GetGeofenceBreachesCsv_WithFilter_ReturnsCsv()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Csv Geo Asset" };
            var device = new Device { Identifier = "CSV-GEO-1", Asset = asset };
            var geofence = new Geofence
            {
                Name = "Depot, North",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 10000,
                IsActive = true
            };
            var observation = new Observation
            {
                Id = Guid.NewGuid(),
                Device = device,
                ObservedAt = DateTime.UtcNow.AddMinutes(-5),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-5),
                Latitude = 51.5074,
                Longitude = -0.1278
            };
            db.Assets.Add(asset);
            db.Devices.Add(device);
            db.Geofences.Add(geofence);
            db.Observations.Add(observation);
            db.GeofenceBreaches.Add(new GeofenceBreach
            {
                Id = Guid.NewGuid(),
                Observation = observation,
                Device = device,
                Asset = asset,
                Geofence = geofence,
                EventType = GeofenceBreachEventType.Enter,
                DetectedAt = DateTime.UtcNow.AddMinutes(-4)
            });
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/geofences/breaches?deviceId={deviceId}&format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("Id,ObservationId,GeofenceId,GeofenceName");
        csv.Should().Contain("CSV-GEO-1");
        csv.Should().Contain("\"Depot, North\"");
    }

    [Fact]
    public async Task GetGeofenceBreachesCsv_WithoutFilter_ReturnsValidationProblem()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/geofences/breaches?format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
