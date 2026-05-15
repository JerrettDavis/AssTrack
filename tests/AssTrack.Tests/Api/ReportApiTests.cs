using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class ReportApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ReportApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUtilizationReport_ReturnsMovementSummaryByDevice()
    {
        await _factory.ResetDatabaseAsync();

        var start = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        Guid assetId;
        Guid deviceId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Service Van 12", AssetClass = AssetClasses.Vehicle };
            var device = new Device { Identifier = "van-12-tracker", Asset = asset };
            dbContext.Assets.Add(asset);
            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync();

            assetId = asset.Id;
            deviceId = device.Id;

            dbContext.Observations.AddRange(
                new Observation { DeviceId = device.Id, ObservedAt = start, ReceivedAt = start, Latitude = 36.0000, Longitude = -95.0000, SpeedKmh = 0 },
                new Observation { DeviceId = device.Id, ObservedAt = start.AddMinutes(10), ReceivedAt = start.AddMinutes(10), Latitude = 36.0100, Longitude = -95.0000, SpeedKmh = 42 },
                new Observation { DeviceId = device.Id, ObservedAt = start.AddMinutes(20), ReceivedAt = start.AddMinutes(20), Latitude = 36.0200, Longitude = -95.0000, SpeedKmh = 45 },
                new Observation { DeviceId = device.Id, ObservedAt = start.AddMinutes(30), ReceivedAt = start.AddMinutes(30), Latitude = 36.0200, Longitude = -95.0000, SpeedKmh = 0 });
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var report = await client.GetFromJsonAsync<UtilizationReportDto>(
            $"/api/reports/utilization?from={Uri.EscapeDataString(start.AddMinutes(-1).ToString("O"))}&to={Uri.EscapeDataString(start.AddMinutes(31).ToString("O"))}");

        report.Should().NotBeNull();
        report!.AssetCount.Should().Be(1);
        report.DeviceCount.Should().Be(1);
        report.ObservationCount.Should().Be(4);
        report.TotalDistanceKm.Should().BeGreaterThan(2.0);
        report.TotalMovingMinutes.Should().Be(20);
        report.TotalIdleMinutes.Should().Be(10);
        report.Items.Should().ContainSingle();
        report.Items[0].AssetId.Should().Be(assetId);
        report.Items[0].DeviceId.Should().Be(deviceId);
        report.Items[0].AssetName.Should().Be("Service Van 12");
        report.Items[0].StopCount.Should().Be(1);
        report.Items[0].MaxSpeedKmh.Should().Be(45);
    }

    [Fact]
    public async Task GetUtilizationReport_FiltersByAsset()
    {
        await _factory.ResetDatabaseAsync();

        var start = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        Guid includedAssetId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var includedAsset = new Asset { Name = "Generator A", AssetClass = AssetClasses.Equipment };
            var excludedAsset = new Asset { Name = "Generator B", AssetClass = AssetClasses.Equipment };
            var includedDevice = new Device { Identifier = "gen-a", Asset = includedAsset };
            var excludedDevice = new Device { Identifier = "gen-b", Asset = excludedAsset };
            dbContext.AddRange(includedAsset, excludedAsset, includedDevice, excludedDevice);
            await dbContext.SaveChangesAsync();

            includedAssetId = includedAsset.Id;

            dbContext.Observations.AddRange(
                new Observation { DeviceId = includedDevice.Id, ObservedAt = start, ReceivedAt = start, Latitude = 36.0, Longitude = -95.0 },
                new Observation { DeviceId = includedDevice.Id, ObservedAt = start.AddMinutes(5), ReceivedAt = start.AddMinutes(5), Latitude = 36.01, Longitude = -95.0 },
                new Observation { DeviceId = excludedDevice.Id, ObservedAt = start, ReceivedAt = start, Latitude = 40.0, Longitude = -75.0 },
                new Observation { DeviceId = excludedDevice.Id, ObservedAt = start.AddMinutes(5), ReceivedAt = start.AddMinutes(5), Latitude = 40.01, Longitude = -75.0 });
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var report = await client.GetFromJsonAsync<UtilizationReportDto>(
            $"/api/reports/utilization?assetId={includedAssetId}&from={Uri.EscapeDataString(start.AddMinutes(-1).ToString("O"))}&to={Uri.EscapeDataString(start.AddMinutes(10).ToString("O"))}");

        report.Should().NotBeNull();
        report!.Items.Should().ContainSingle();
        report.Items[0].AssetId.Should().Be(includedAssetId);
        report.Items[0].DeviceIdentifier.Should().Be("gen-a");
    }

    [Fact]
    public async Task GetUtilizationReport_ReturnsValidationProblemForInvalidRange()
    {
        await _factory.ResetDatabaseAsync();

        var start = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync(
            $"/api/reports/utilization?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddMinutes(-1).ToString("O"))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
