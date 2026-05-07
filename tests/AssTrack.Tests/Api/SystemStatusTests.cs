using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SystemStatusTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SystemStatusTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSystemStatus_WithApiKey_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemStatus_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemStatus_SimulationEnabled_IsBool()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        ((object)result!.SimulationEnabled).Should().BeOfType<bool>();
    }

    [Fact]
    public async Task GetSystemStatus_DatabaseProvider_IsSQLite()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.DatabaseProvider.Should().Be("SQLite");
    }

    [Fact]
    public async Task GetSystemStatus_RateLimitPermitLimit_IsPositive()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.RateLimitPermitLimit.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSystemStatus_IngestApiKeyConfigured_IsTrue_WhenSet()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.IngestApiKeyConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task CleanNullIsland_DryRun_DoesNotDeleteObservations()
    {
        await _factory.ResetDatabaseAsync();
        await SeedGoodAndNoisyObservationsAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=true", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ObservationCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingObservations.Should().Be(1);
        result.DeletedObservations.Should().Be(0);
        result.AffectedDevices.Should().Be(1);
        result.ResetGeofenceStates.Should().Be(1);
        result.DryRun.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await dbContext.Observations.CountAsync()).Should().Be(2);
        (await dbContext.DeviceGeofenceStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CleanNullIsland_DeletesNoise_AndResetsAffectedGeofenceState()
    {
        await _factory.ResetDatabaseAsync();
        var deviceId = await SeedGoodAndNoisyObservationsAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=false", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ObservationCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingObservations.Should().Be(1);
        result.DeletedObservations.Should().Be(1);
        result.AffectedDevices.Should().Be(1);
        result.ResetGeofenceStates.Should().Be(1);
        result.DryRun.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var remaining = await dbContext.Observations.SingleAsync();
        remaining.DeviceId.Should().Be(deviceId);
        remaining.Latitude.Should().Be(36.0595);
        (await dbContext.DeviceGeofenceStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanAutoCreatedProviderAssets_DetachesMeshtasticDevices_AndDeletesGeneratedAssets()
    {
        await _factory.ResetDatabaseAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var feed = new IntegrationFeed { Name = "JDH Meshtastic", Provider = "meshtastic" };
            var generatedAsset = new Asset { Name = "!12f4fb74", Category = "Mesh node" };
            var manualAsset = new Asset { Name = "JDH Dev Node 0001", Category = "Mesh node", Description = "User enrolled asset" };
            dbContext.IntegrationFeeds.Add(feed);
            dbContext.Assets.AddRange(generatedAsset, manualAsset);
            dbContext.Devices.AddRange(
                new Device
                {
                    Identifier = "meshtastic:!12f4fb74",
                    Provider = "meshtastic",
                    Protocol = "meshtastic",
                    ExternalId = "!12f4fb74",
                    IntegrationFeed = feed,
                    Asset = generatedAsset
                },
                new Device
                {
                    Identifier = "meshtastic:!f6701924",
                    Provider = "meshtastic",
                    Protocol = "meshtastic",
                    ExternalId = "!f6701924",
                    IntegrationFeed = feed,
                    Asset = manualAsset
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-auto-created-provider-assets?dryRun=false", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AutoCreatedAssetCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingAssets.Should().Be(1);
        result.DeletedAssets.Should().Be(1);
        result.DetachedDevices.Should().Be(1);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await verifyDb.Assets.SingleAsync()).Name.Should().Be("JDH Dev Node 0001");
        var detached = await verifyDb.Devices.SingleAsync(device => device.Identifier == "meshtastic:!12f4fb74");
        detached.AssetId.Should().BeNull();
        var retained = await verifyDb.Devices.SingleAsync(device => device.Identifier == "meshtastic:!f6701924");
        retained.AssetId.Should().NotBeNull();
    }

    private async Task<Guid> SeedGoodAndNoisyObservationsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var device = new Device { Identifier = $"cleanup-dev-{Guid.NewGuid():N}" };
        var geofence = new Geofence
        {
            Name = "Cleanup state",
            CenterLatitude = 36.0595,
            CenterLongitude = -95.8976,
            RadiusMeters = 500,
            IsActive = true
        };
        dbContext.Devices.Add(device);
        dbContext.Geofences.Add(geofence);
        await dbContext.SaveChangesAsync();

        dbContext.Observations.AddRange(
            new Observation
            {
                DeviceId = device.Id,
                ObservedAt = DateTime.UtcNow.AddMinutes(-10),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                Latitude = 36.0595,
                Longitude = -95.8976
            },
            new Observation
            {
                DeviceId = device.Id,
                ObservedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 0,
                Longitude = 0
            });
        dbContext.DeviceGeofenceStates.Add(new DeviceGeofenceState
        {
            DeviceId = device.Id,
            GeofenceId = geofence.Id,
            IsInside = false,
            LastObservationAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return device.Id;
    }
}
