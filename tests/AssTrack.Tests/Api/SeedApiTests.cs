using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SeedApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SeedApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Seed_CreatesAssets_Devices_Geofences()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AssetsCreated.Should().Be(3);
        result.DevicesCreated.Should().Be(3);
        result.GeofencesCreated.Should().Be(2);
        result.AlreadySeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Seed_IsIdempotent_ReturnAlreadySeeded_OnSecondCall()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AlreadySeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Seed_WithReset_WipesAndReseeds()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        result.Should().NotBeNull();
        result!.AlreadySeeded.Should().BeFalse();
        result.ResetPerformed.Should().BeTrue();
        result.AssetsCreated.Should().Be(3);
    }

    [Fact]
    public async Task Seed_Reset_DoesNotTouch_NonSeededRecords()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Manual Asset", null, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));

        var assetsResponse = await client.GetAsync("/api/assets");
        assetsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assets = await assetsResponse.Content.ReadFromJsonAsync<AssetDto[]>();
        assets.Should().NotBeNull();
        assets!.Should().Contain(a => a.Name == "Manual Asset");
        assets.Should().ContainSingle(a => a.Name == "Fleet Van Alpha" && a.IsSeeded);
    }

    [Fact]
    public async Task Seed_WhenSimulationDisabled_Returns403()
    {
        await _factory.ResetDatabaseAsync();
        using var disabledFactory = new DisabledSeedWebApplicationFactory();
        using var client = disabledFactory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_HasData_IsFalse_WhenNoAssets()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        status.Should().NotBeNull();
        status!.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task Status_HasData_IsTrue_AfterSeed()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        status.Should().NotBeNull();
        status!.HasData.Should().BeTrue();
    }

    [Fact]
    public async Task Seed_Reset_DoesNotFail_WhenSeededDevicesHaveSpeedAlertsAndGeofenceBreaches()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // Seed first so seeded devices and geofences exist.
        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));

        Guid seededSpeedAlertId;
        Guid seededGeofenceBreachId;

        // Directly inject a SpeedAlert and GeofenceBreach for a seeded device.
        // Both link to the seeded device via DeviceId (DeleteBehavior.Restrict), so without the
        // fix the subsequent device deletion would raise an FK violation.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

            var seededDevice = await db.Devices.FirstAsync(d => d.IsSeeded);

            // Use a non-seeded geofence so the FK is not auto-deleted via the seeded-geofence cleanup.
            var nonSeededGeofence = new Geofence
            {
                Name = "NonSeededGeofence-ResetTest",
                CenterLatitude = 51.0,
                CenterLongitude = -0.1,
                RadiusMeters = 100,
                IsActive = true,
                IsSeeded = false,
                CreatedAt = DateTime.UtcNow,
            };
            db.Geofences.Add(nonSeededGeofence);
            await db.SaveChangesAsync();

            var obs = new Observation
            {
                DeviceId = seededDevice.Id,
                ObservedAt = DateTime.UtcNow.AddMinutes(-5),
                Latitude = 51.5,
                Longitude = -0.1,
            };
            db.Observations.Add(obs);
            await db.SaveChangesAsync();

            var speedAlert = new SpeedAlert
            {
                ObservationId = obs.Id,
                DeviceId = seededDevice.Id,
                ObservedSpeedKmh = 120,
                ThresholdKmh = 80,
                TriggeredAt = DateTime.UtcNow,
            };
            db.SpeedAlerts.Add(speedAlert);

            var geofenceBreach = new GeofenceBreach
            {
                ObservationId = obs.Id,
                DeviceId = seededDevice.Id,
                GeofenceId = nonSeededGeofence.Id,
                DetectedAt = DateTime.UtcNow,
            };
            db.GeofenceBreaches.Add(geofenceBreach);
            await db.SaveChangesAsync();

            seededSpeedAlertId = speedAlert.Id;
            seededGeofenceBreachId = geofenceBreach.Id;
        }

        // Reset must succeed without FK constraint violations.
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Both records linked to the now-deleted seeded device must be gone.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            (await db.SpeedAlerts.AnyAsync(s => s.Id == seededSpeedAlertId)).Should().BeFalse(
                "SpeedAlert for a seeded device must be deleted during reset");
            (await db.GeofenceBreaches.AnyAsync(b => b.Id == seededGeofenceBreachId)).Should().BeFalse(
                "GeofenceBreach for a seeded device must be deleted during reset");
        }
    }

    [Fact]
    public async Task Seed_Reset_DoesNotDelete_NonSeededDeviceAlerts()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        Guid nonSeededSpeedAlertId;
        Guid nonSeededGeofenceBreachId;

        // Create a non-seeded device with its own SpeedAlert and GeofenceBreach.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

            var nonSeededDevice = new Device
            {
                Identifier = "manual-device-reset-test",
                Label = "Manual Device",
                Protocol = "https",
                IsSeeded = false,
                CreatedAt = DateTime.UtcNow,
            };
            db.Devices.Add(nonSeededDevice);

            var nonSeededGeofence = new Geofence
            {
                Name = "ManualGeofence-NonSeeded",
                CenterLatitude = 52.0,
                CenterLongitude = -1.5,
                RadiusMeters = 200,
                IsActive = true,
                IsSeeded = false,
                CreatedAt = DateTime.UtcNow,
            };
            db.Geofences.Add(nonSeededGeofence);
            await db.SaveChangesAsync();

            var obs = new Observation
            {
                DeviceId = nonSeededDevice.Id,
                ObservedAt = DateTime.UtcNow.AddMinutes(-10),
                Latitude = 52.0,
                Longitude = -1.5,
            };
            db.Observations.Add(obs);
            await db.SaveChangesAsync();

            var speedAlert = new SpeedAlert
            {
                ObservationId = obs.Id,
                DeviceId = nonSeededDevice.Id,
                ObservedSpeedKmh = 110,
                ThresholdKmh = 90,
                TriggeredAt = DateTime.UtcNow,
            };
            db.SpeedAlerts.Add(speedAlert);

            var geofenceBreach = new GeofenceBreach
            {
                ObservationId = obs.Id,
                DeviceId = nonSeededDevice.Id,
                GeofenceId = nonSeededGeofence.Id,
                DetectedAt = DateTime.UtcNow,
            };
            db.GeofenceBreaches.Add(geofenceBreach);
            await db.SaveChangesAsync();

            nonSeededSpeedAlertId = speedAlert.Id;
            nonSeededGeofenceBreachId = geofenceBreach.Id;
        }

        // Seed then reset; should not affect the non-seeded records above.
        await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: false));
        var response = await client.PostAsJsonAsync("/api/system/seed", new SeedRequest(Reset: true));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            (await db.SpeedAlerts.AnyAsync(s => s.Id == nonSeededSpeedAlertId)).Should().BeTrue(
                "SpeedAlert for a non-seeded device must survive seed reset");
            (await db.GeofenceBreaches.AnyAsync(b => b.Id == nonSeededGeofenceBreachId)).Should().BeTrue(
                "GeofenceBreach for a non-seeded device must survive seed reset");
        }
    }
}

file sealed class DisabledSeedWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:Enabled"] = "false"
            });
        });
    }
}
