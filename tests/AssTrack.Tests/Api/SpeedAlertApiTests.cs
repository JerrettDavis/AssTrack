using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SpeedAlertApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task PostFastObservation_CreatesSpeedAlert()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "AlertAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device
        {
            Id = Guid.NewGuid(),
            Identifier = $"ALERT-{Guid.NewGuid():N}",
            Protocol = "MQTT",
            CreatedAt = DateTime.UtcNow,
            AssetId = asset.Id
        };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var payload = new
        {
            DeviceId = device.Id,
            Latitude = 51.5,
            Longitude = -0.1,
            SpeedKmh = 130.0,
            ObservedAt = DateTime.UtcNow
        };
        var post = await client.PostAsJsonAsync("/api/observations", payload);
        post.EnsureSuccessStatusCode();

        var alerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        Assert.NotNull(alerts);
        var alert = alerts.FirstOrDefault(a => a.GetProperty("deviceId").GetString() == device.Id.ToString());
        Assert.NotEqual(default, alert);
        Assert.Equal(130.0, alert.GetProperty("observedSpeedKmh").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task PostObservation_WithCustomThreshold_CreatesAlertWhenSpeedExceedsThreshold()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "CustomThresholdAsset", SpeedThresholdKmh = 80.0, CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device
        {
            Id = Guid.NewGuid(),
            Identifier = $"THRESH-{Guid.NewGuid():N}",
            Protocol = "MQTT",
            CreatedAt = DateTime.UtcNow,
            AssetId = asset.Id
        };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var payload = new { DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 90.0, ObservedAt = DateTime.UtcNow };
        var post = await client.PostAsJsonAsync("/api/observations", payload);
        post.EnsureSuccessStatusCode();

        var alerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        alerts.Should().NotBeNull();
        var alert = alerts!.FirstOrDefault(a => a.GetProperty("deviceId").GetString() == device.Id.ToString());
        alert.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        alert.GetProperty("observedSpeedKmh").GetDouble().Should().Be(90.0);
        alert.GetProperty("thresholdKmh").GetDouble().Should().Be(80.0);
    }

    [Fact]
    public async Task PostObservation_WithCustomThreshold_NoAlertWhenSpeedBelowThreshold()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "CustomThresholdAsset2", SpeedThresholdKmh = 80.0, CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device
        {
            Id = Guid.NewGuid(),
            Identifier = $"THRESH2-{Guid.NewGuid():N}",
            Protocol = "MQTT",
            CreatedAt = DateTime.UtcNow,
            AssetId = asset.Id
        };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var payload = new { DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 75.0, ObservedAt = DateTime.UtcNow };
        var post = await client.PostAsJsonAsync("/api/observations", payload);
        post.EnsureSuccessStatusCode();

        var alerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        alerts.Should().NotBeNull();
        var alert = alerts!.FirstOrDefault(a => a.GetProperty("deviceId").GetString() == device.Id.ToString());
        alert.ValueKind.Should().Be(JsonValueKind.Undefined, "no alert should be created when speed is below threshold");
    }

    [Fact]
    public async Task AcknowledgeSpeedAlert_SetsAcknowledgementFields()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "AckAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device
        {
            Id = Guid.NewGuid(),
            Identifier = $"ACK-{Guid.NewGuid():N}",
            Protocol = "MQTT",
            CreatedAt = DateTime.UtcNow,
            AssetId = asset.Id
        };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var payload = new { DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 130.0, ObservedAt = DateTime.UtcNow };
        var post = await client.PostAsJsonAsync("/api/observations", payload);
        post.EnsureSuccessStatusCode();

        var alerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        alerts.Should().NotBeNull();
        var alert = alerts!.First(a => a.GetProperty("deviceId").GetString() == device.Id.ToString());
        var alertId = alert.GetProperty("id").GetString();

        var ackResponse = await client.PostAsJsonAsync($"/api/speed-alerts/{alertId}/acknowledge", new { AcknowledgedBy = "operator1" });
        ackResponse.EnsureSuccessStatusCode();

        var updatedAlerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        var updatedAlert = updatedAlerts!.First(a => a.GetProperty("id").GetString() == alertId);
        updatedAlert.GetProperty("acknowledgedBy").GetString().Should().Be("operator1");
        updatedAlert.GetProperty("acknowledgedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }
}
