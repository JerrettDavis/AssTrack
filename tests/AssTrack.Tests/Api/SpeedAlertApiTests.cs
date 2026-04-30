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

    [Fact]
    public async Task GetSpeedAlerts_FilterByUnacknowledged_ReturnsOnlyUnacknowledged()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "FilterAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device { Id = Guid.NewGuid(), Identifier = $"FILTER-{Guid.NewGuid():N}", Protocol = "MQTT", CreatedAt = DateTime.UtcNow, AssetId = asset.Id };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddHours(-1);
        var obs1 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 130.0, ObservedAt = baseTime };
        var obs2 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.6, Longitude = -0.2, SpeedKmh = 135.0, ObservedAt = baseTime.AddMinutes(10) };
        db.Observations.Add(obs1);
        db.Observations.Add(obs2);
        await db.SaveChangesAsync();

        var alert1 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs1.Id, ObservedSpeedKmh = 130.0, ThresholdKmh = 120.0, TriggeredAt = baseTime };
        var alert2 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs2.Id, ObservedSpeedKmh = 135.0, ThresholdKmh = 120.0, TriggeredAt = baseTime.AddMinutes(10) };
        db.SpeedAlerts.Add(alert1);
        db.SpeedAlerts.Add(alert2);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var allAlerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        var firstAlertId = allAlerts!.First(a => a.GetProperty("deviceId").GetString() == device.Id.ToString()).GetProperty("id").GetString();
        await client.PostAsJsonAsync($"/api/speed-alerts/{firstAlertId}/acknowledge", new { AcknowledgedBy = "test" });

        var unackAlerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts?unacknowledged=true");
        unackAlerts.Should().NotBeNull();
        unackAlerts!.Count(a => a.GetProperty("deviceId").GetString() == device.Id.ToString()).Should().Be(1);
    }

    [Fact]
    public async Task BulkAcknowledgeSpeedAlerts_AcknowledgesMultipleAlerts()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "BulkAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device { Id = Guid.NewGuid(), Identifier = $"BULK-{Guid.NewGuid():N}", Protocol = "MQTT", CreatedAt = DateTime.UtcNow, AssetId = asset.Id };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddHours(-1);
        var obs1 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 130.0, ObservedAt = baseTime };
        var obs2 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.6, Longitude = -0.2, SpeedKmh = 135.0, ObservedAt = baseTime.AddMinutes(10) };
        db.Observations.Add(obs1);
        db.Observations.Add(obs2);
        await db.SaveChangesAsync();

        var alert1 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs1.Id, ObservedSpeedKmh = 130.0, ThresholdKmh = 120.0, TriggeredAt = baseTime };
        var alert2 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs2.Id, ObservedSpeedKmh = 135.0, ThresholdKmh = 120.0, TriggeredAt = baseTime.AddMinutes(10) };
        db.SpeedAlerts.Add(alert1);
        db.SpeedAlerts.Add(alert2);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var alerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts");
        var alertIds = alerts!.Where(a => a.GetProperty("deviceId").GetString() == device.Id.ToString()).Select(a => a.GetProperty("id").GetString()).ToList();

        var bulkResponse = await client.PostAsJsonAsync("/api/speed-alerts/bulk-acknowledge", new { Ids = alertIds, AcknowledgedBy = "bulk-operator" });
        bulkResponse.EnsureSuccessStatusCode();
        var result = await bulkResponse.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("count").GetInt32().Should().Be(2);

        var unackAlerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts?unacknowledged=true");
        unackAlerts!.Count(a => a.GetProperty("deviceId").GetString() == device.Id.ToString()).Should().Be(0);
    }

    [Fact]
    public async Task GetAlertSummary_ReturnsUnacknowledgedCounts()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "SummaryAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device { Id = Guid.NewGuid(), Identifier = $"SUMMARY-{Guid.NewGuid():N}", Protocol = "MQTT", CreatedAt = DateTime.UtcNow, AssetId = asset.Id };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddHours(-1);
        var obs1 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 130.0, ObservedAt = baseTime };
        var obs2 = new AssTrack.Domain.Models.Observation { Id = Guid.NewGuid(), DeviceId = device.Id, Latitude = 51.6, Longitude = -0.2, SpeedKmh = 135.0, ObservedAt = baseTime.AddMinutes(10) };
        db.Observations.Add(obs1);
        db.Observations.Add(obs2);
        await db.SaveChangesAsync();

        var alert1 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs1.Id, ObservedSpeedKmh = 130.0, ThresholdKmh = 120.0, TriggeredAt = baseTime };
        var alert2 = new AssTrack.Domain.Models.SpeedAlert { Id = Guid.NewGuid(), DeviceId = device.Id, AssetId = asset.Id, ObservationId = obs2.Id, ObservedSpeedKmh = 135.0, ThresholdKmh = 120.0, TriggeredAt = baseTime.AddMinutes(10) };
        db.SpeedAlerts.Add(alert1);
        db.SpeedAlerts.Add(alert2);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/alerts/summary");
        summary.GetProperty("unacknowledgedSpeedAlerts").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetSpeedAlerts_WithLimitAndSince_FiltersCorrectly()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();

        var asset = new AssTrack.Domain.Models.Asset { Id = Guid.NewGuid(), Name = "LimitAsset", CreatedAt = DateTime.UtcNow };
        var device = new AssTrack.Domain.Models.Device { Id = Guid.NewGuid(), Identifier = $"LIMIT-{Guid.NewGuid():N}", Protocol = "MQTT", CreatedAt = DateTime.UtcNow, AssetId = asset.Id };
        db.Assets.Add(asset);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/observations", new { DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 130.0, ObservedAt = DateTime.UtcNow });
        await client.PostAsJsonAsync("/api/observations", new { DeviceId = device.Id, Latitude = 51.5, Longitude = -0.1, SpeedKmh = 135.0, ObservedAt = DateTime.UtcNow.AddMinutes(1) });

        var limitedAlerts = await client.GetFromJsonAsync<List<JsonElement>>("/api/speed-alerts?limit=1");
        limitedAlerts.Should().NotBeNull();
        limitedAlerts!.Count.Should().BeLessThanOrEqualTo(1);
    }
}
