using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class MaintenanceApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MaintenanceApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMaintenanceSchedule_Should_CreateAndReturnCurrentSchedule()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var assetResponse = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 22", null, "Vehicle", AssetClass: "vehicle"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var response = await client.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(
            asset!.Id,
            "Oil service",
            "oil",
            IntervalDays: 90,
            IntervalOdometerKm: 5000,
            LastServiceAt: DateTime.UtcNow.AddDays(-10),
            LastOdometerKm: 10000));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<MaintenanceScheduleDto>();
        schedule.Should().NotBeNull();
        schedule!.AssetId.Should().Be(asset.Id);
        schedule.AssetName.Should().Be("Fleet Van 22");
        schedule.Status.Should().Be("current");
        schedule.NextOdometerKm.Should().Be(15000);
    }

    [Fact]
    public async Task GetMaintenanceSchedules_Should_UseLatestOdometerForDueStatus()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 23", null, "Vehicle", AssetClass: "vehicle"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        await operatorClient.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(
            asset!.Id,
            "Mileage inspection",
            "inspection",
            IntervalOdometerKm: 1000,
            LastOdometerKm: 20000));

        await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            asset.Id,
            null,
            null,
            null,
            "odometer",
            "Odometer",
            21000,
            null,
            "km",
            DateTime.UtcNow,
            null));

        var schedules = await operatorClient.GetFromJsonAsync<List<MaintenanceScheduleDto>>($"/api/maintenance/schedules?assetId={asset.Id}");

        schedules.Should().ContainSingle();
        schedules![0].Status.Should().Be("due");
        schedules[0].LatestOdometerKm.Should().Be(21000);
    }

    [Fact]
    public async Task PostMaintenanceSchedule_WithoutAnyInterval_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var assetResponse = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Generator 1", null, "Equipment", AssetClass: "equipment"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var response = await client.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(asset!.Id, "Inspection"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
