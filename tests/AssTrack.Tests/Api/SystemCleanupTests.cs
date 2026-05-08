using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SystemCleanupTests
{
    [Fact]
    public async Task CleanE2EData_RemovesE2ERecords_InTesting()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.ResetDatabaseAsync();
        var client = factory.CreateAuthenticatedClient();

        var assetId = await CreateAndReturnIdAsync(client, "/api/assets", new
        {
            name = "E2E Cleanup Asset",
            description = "E2E test asset",
            category = "Vehicle"
        });
        var deviceId = await CreateAndReturnIdAsync(client, "/api/devices", new
        {
            identifier = $"E2E-CLEANUP-{Guid.NewGuid():N}",
            label = "E2E Cleanup Device",
            protocol = "https",
            tags = "e2e",
            assetId
        });
        await CreateAndReturnIdAsync(client, "/api/sensors/readings", new
        {
            assetId,
            deviceId,
            sensorType = "odometer",
            numericValue = 1234,
            observedAt = DateTime.UtcNow
        });
        await CreateAndReturnIdAsync(client, "/api/observations", new
        {
            deviceId,
            observedAt = DateTime.UtcNow,
            latitude = 40.7128,
            longitude = -74.0060,
            speedKmh = 150
        });
        await CreateAndReturnIdAsync(client, "/api/maintenance/schedules", new
        {
            assetId,
            title = "E2E Cleanup Maintenance",
            serviceType = "inspection",
            intervalDays = 30
        });

        var response = await client.DeleteAsync("/api/system/maintenance/e2e-data");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await db.Assets.AnyAsync(asset => EF.Functions.Like(asset.Name, "E2E %"))).Should().BeFalse();
        (await db.Devices.AnyAsync(device => EF.Functions.Like(device.Identifier, "E2E-%") || EF.Functions.Like(device.Tags ?? string.Empty, "%e2e%"))).Should().BeFalse();
        (await db.Observations.AnyAsync()).Should().BeFalse();
        (await db.SensorReadings.AnyAsync()).Should().BeFalse();
        (await db.SpeedAlerts.AnyAsync()).Should().BeFalse();
        (await db.MaintenanceSchedules.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CleanE2EData_IsForbidden_InProduction()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"]);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/system/maintenance/e2e-data");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<string> CreateAndReturnIdAsync(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString() ?? throw new InvalidOperationException($"No id returned from {path}.");
    }
}
