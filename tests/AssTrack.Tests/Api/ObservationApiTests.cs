using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class ObservationApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ObservationApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostObservation_Should_CreateObservation_AndLatestEndpointReturnsAssetName()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Generator A", Category = "Equipment" };
            var device = new Device { Identifier = "dev-100", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 40.7128, -74.0060, 12, 3, 130, 180, "{\"battery\":82}");

        var response = await client.PostAsJsonAsync("/api/observations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var latest = await client.GetFromJsonAsync<ObservationDto>($"/api/observations/latest/{deviceId}");
        latest.Should().NotBeNull();
        latest!.AssetName.Should().Be("Generator A");
        latest.DeviceIdentifier.Should().Be("dev-100");
    }

    [Fact]
    public async Task LatestObservation_Should_SerializeStoredDateTimesAsUtc()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        var observedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-2), DateTimeKind.Unspecified);
        var receivedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-utc" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            dbContext.Observations.Add(new Observation
            {
                DeviceId = device.Id,
                ObservedAt = observedAt,
                ReceivedAt = receivedAt,
                Latitude = 36.0595,
                Longitude = -95.8976
            });
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/observations/latest/{deviceId}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("observedAt").GetString().Should().EndWith("Z");
        document.RootElement.GetProperty("receivedAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task PostObservation_Should_CreateSpeedAlert_WhenThresholdExceeded()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-200" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, 5, 121, 45, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var alerts = await verificationDbContext.SpeedAlerts.ToListAsync();
        alerts.Should().ContainSingle();
        alerts[0].ObservedSpeedKmh.Should().Be(121);
    }

    [Fact]
    public async Task PostObservation_Ingest_Should_CreateObservation_AndLatestEndpointReturnsAssetName()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Generator B", Category = "Equipment" };
            var device = new Device { Identifier = "dev-101", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 40.7128, -74.0060, 12, 3, 130, 180, "{\"battery\":75}");

        var response = await client.PostAsJsonAsync("/api/observations/ingest", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var latest = await client.GetFromJsonAsync<ObservationDto>($"/api/observations/latest/{deviceId}");
        latest.Should().NotBeNull();
        latest!.AssetName.Should().Be("Generator B");
        latest.DeviceIdentifier.Should().Be("dev-101");
    }

    [Fact]
    public async Task PostObservation_Ingest_Should_CreateSpeedAlert_WhenThresholdExceeded()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-202" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, 5, 125, 45, null);
        var response = await client.PostAsJsonAsync("/api/observations/ingest", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var alerts = await verificationDbContext.SpeedAlerts.ToListAsync();
        alerts.Should().ContainSingle();
        alerts[0].ObservedSpeedKmh.Should().Be(125);
    }

    [Fact]
    public async Task PostObservation_Should_Return422_WhenDeviceNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();
        var request = new CreateObservationRequest(Guid.NewGuid(), DateTime.UtcNow, 0, 0, null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/api/observations", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Ingest_InvalidLatitude_Returns422()
    {
        var request = new
        {
            deviceIdentifier = "VALID_DEV",
            latitude = 91.0,
            longitude = 0.0,
            speedKmh = 50.0,
            observedAt = DateTime.UtcNow
        };
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/observations", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_InvalidLongitude_Returns422()
    {
        var request = new
        {
            deviceIdentifier = "VALID_DEV",
            latitude = 0.0,
            longitude = 181.0,
            speedKmh = 50.0,
            observedAt = DateTime.UtcNow
        };
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/observations", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_NullIslandNoise_Returns422()
    {
        await _factory.ResetDatabaseAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            dbContext.Devices.Add(new Device { Identifier = "null-island-dev" });
            await dbContext.SaveChangesAsync();
        }

        var request = new
        {
            deviceIdentifier = "null-island-dev",
            latitude = 0.00001,
            longitude = -0.00001,
            observedAt = DateTime.UtcNow
        };

        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/observations", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LatestPosition_IgnoresNullIslandNoise_AndReturnsPreviousGoodObservation()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "latest-noise-dev" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;

            dbContext.Observations.AddRange(
                new Observation
                {
                    DeviceId = device.Id,
                    ObservedAt = DateTime.UtcNow.AddMinutes(-5),
                    ReceivedAt = DateTime.UtcNow.AddMinutes(-5),
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
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var latest = await client.GetFromJsonAsync<ObservationDto>($"/api/observations/latest/{deviceId}");
        var positions = await client.GetFromJsonAsync<List<ObservationDto>>("/api/observations/latest-positions");

        latest.Should().NotBeNull();
        latest!.Latitude.Should().Be(36.0595);
        latest.Longitude.Should().Be(-95.8976);
        positions.Should().ContainSingle(item => item.DeviceId == deviceId && item.Latitude == 36.0595);
    }

    [Fact]
    public async Task Ingest_NegativeSpeed_Returns422()
    {
        var request = new
        {
            deviceIdentifier = "VALID_DEV",
            latitude = 0.0,
            longitude = 0.0,
            speedKmh = -1.0,
            observedAt = DateTime.UtcNow
        };
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/observations", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostObservation_Should_NotCreateDuplicateSpeedAlert_WithinCooldown()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-cooldown-01" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var request1 = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 130, null, null);
        var response1 = await client.PostAsJsonAsync("/api/observations", request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var request2 = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(-1), 51.5074, -0.1278, null, null, 135, null, null);
        var response2 = await client.PostAsJsonAsync("/api/observations", request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var alerts = await verificationDbContext.SpeedAlerts.ToListAsync();
        alerts.Should().ContainSingle("second speed alert within cooldown window should be suppressed");
    }

    [Fact]
    public async Task DuplicateIngest_Returns200_WithExistingObservation_AndCountRemainsOne()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-duplicate-01" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var observedAt = DateTime.UtcNow;
        var request = new CreateObservationRequest(deviceId, observedAt, 51.5074, -0.1278, null, null, 80, null, null);

        var response1 = await client.PostAsJsonAsync("/api/observations", request);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response1.Content.ReadFromJsonAsync<ObservationDto>();

        var response2 = await client.PostAsJsonAsync("/api/observations", request);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var existing = await response2.Content.ReadFromJsonAsync<ObservationDto>();

        existing.Should().NotBeNull();
        existing!.Id.Should().Be(created!.Id);
        existing!.DeviceIdentifier.Should().Be("dev-duplicate-01");

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var count = await verificationDbContext.Observations.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task OutOfOrderObservation_DoesNotOverwriteNewerGeofenceState_AndOnlyRecordsInitialEnter()
    {
        await _factory.ResetDatabaseAsync();

        Guid deviceId;
        Guid geofenceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-out-of-order-01" };
            var geofence = new Geofence
            {
                Name = "Out Of Order Zone",
                CenterLatitude = 51.5074,
                CenterLongitude = -0.1278,
                RadiusMeters = 5000,
                IsActive = true
            };
            dbContext.Devices.Add(device);
            dbContext.Geofences.Add(geofence);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
            geofenceId = geofence.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var newerObservedAt = DateTime.UtcNow;
        var olderObservedAt = newerObservedAt.AddSeconds(-10);

        var newerRequest = new CreateObservationRequest(deviceId, newerObservedAt, 51.5074, -0.1278, null, null, 50, null, null);
        var newerResponse = await client.PostAsJsonAsync("/api/observations", newerRequest);
        newerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var olderRequest = new CreateObservationRequest(deviceId, olderObservedAt, 40.7128, -74.0060, null, null, 50, null, null);
        var olderResponse = await client.PostAsJsonAsync("/api/observations", olderRequest);
        olderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var breaches = await verificationDbContext.GeofenceBreaches.OrderBy(b => b.DetectedAt).ToListAsync();
        breaches.Should().ContainSingle();
        breaches[0].EventType.Should().Be(GeofenceBreachEventType.Enter);

        var state = await verificationDbContext.DeviceGeofenceStates.FirstAsync(x => x.DeviceId == deviceId && x.GeofenceId == geofenceId);
        state.IsInside.Should().BeTrue();
        state.LastObservationAt.Should().Be(newerObservedAt);
    }
}

public class RateLimitTests : IClassFixture<RateLimitedTestWebApplicationFactory>
{
    private readonly RateLimitedTestWebApplicationFactory _factory;

    public RateLimitTests(RateLimitedTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RateLimiting_Returns429_AfterLimitExceeded()
    {
        await _factory.ResetDatabaseAsync();
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var device = new Device { Identifier = "dev-rate-limit-01" };
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();
            deviceId = device.Id;
        }

        using var client = _factory.CreateAuthenticatedClient();
        var firstRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, null, 50, null, null);
        var firstResponse = await client.PostAsJsonAsync("/api/observations", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondRequest = new CreateObservationRequest(deviceId, DateTime.UtcNow.AddSeconds(1), 51.5074, -0.1278, null, null, 50, null, null);
        var secondResponse = await client.PostAsJsonAsync("/api/observations", secondRequest);
        secondResponse.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
