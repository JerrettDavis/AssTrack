using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Infrastructure.Data;
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
}
