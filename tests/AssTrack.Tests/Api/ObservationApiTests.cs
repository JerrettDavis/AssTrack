using System.Net;
using System.Net.Http.Json;
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

        using var client = _factory.CreateClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 40.7128, -74.0060, 12, 3, 130, 180, "{\"battery\":82}");

        var response = await client.PostAsJsonAsync("/api/observations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var latest = await client.GetFromJsonAsync<ObservationDto>($"/api/observations/latest/{deviceId}");
        latest.Should().NotBeNull();
        latest!.AssetName.Should().Be("Generator A");
        latest.DeviceIdentifier.Should().Be("dev-100");
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

        using var client = _factory.CreateClient();
        var request = new CreateObservationRequest(deviceId, DateTime.UtcNow, 51.5074, -0.1278, null, 5, 121, 45, null);
        var response = await client.PostAsJsonAsync("/api/observations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var alerts = await verificationDbContext.SpeedAlerts.ToListAsync();
        alerts.Should().ContainSingle();
        alerts[0].ObservedSpeedKmh.Should().Be(121);
    }
}
