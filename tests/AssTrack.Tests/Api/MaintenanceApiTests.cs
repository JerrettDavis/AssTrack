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

    [Fact]
    public async Task GetMaintenanceSchedules_Should_UseDiagnosticEventForDueStatus()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 25", null, "Vehicle", AssetClass: "vehicle"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        await operatorClient.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(
            asset!.Id,
            "Check engine diagnostic",
            "inspection",
            DiagnosticSensorType: "diagnostic_code",
            DiagnosticTextContains: "P0420",
            LastServiceAt: DateTime.UtcNow.AddDays(-1)));

        await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            asset.Id,
            null,
            null,
            null,
            "diagnostic_code",
            "Diagnostic code",
            null,
            "P0420 catalyst efficiency below threshold",
            null,
            DateTime.UtcNow,
            null));

        var schedules = await operatorClient.GetFromJsonAsync<List<MaintenanceScheduleDto>>($"/api/maintenance/schedules?assetId={asset.Id}");

        schedules.Should().ContainSingle();
        schedules![0].Status.Should().Be("due");
        schedules[0].DiagnosticSensorType.Should().Be("diagnostic_code");
        schedules[0].LatestDiagnosticAt.Should().NotBeNull();
        schedules[0].LatestDiagnosticValue.Should().Contain("P0420");
    }

    [Fact]
    public async Task GetMaintenanceReminders_Should_ReturnDiagnosticReminder()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Generator 2", null, "Equipment", AssetClass: "equipment"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var scheduleResponse = await operatorClient.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(
            asset!.Id,
            "Low oil pressure diagnostic",
            "inspection",
            DiagnosticSensorType: "diagnostic_event",
            DiagnosticTextContains: "low oil"));
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<MaintenanceScheduleDto>();

        await ingestClient.PostAsJsonAsync("/api/sensors/readings", new CreateSensorReadingRequest(
            asset.Id,
            null,
            null,
            null,
            "diagnostic_event",
            "Diagnostic event",
            null,
            "low oil pressure",
            null,
            DateTime.UtcNow,
            null));

        var reminders = await operatorClient.GetFromJsonAsync<List<MaintenanceReminderDto>>($"/api/maintenance/reminders?assetId={asset.Id}");

        reminders.Should().ContainSingle(x =>
            x.ScheduleId == schedule!.Id &&
            x.Status == "due" &&
            x.Reason == "Diagnostic event" &&
            x.DiagnosticValue == "low oil pressure");
    }

    [Fact]
    public async Task CompleteMaintenanceSchedule_Should_CreateRecordAndResetDueBaseline()
    {
        await _factory.ResetDatabaseAsync();
        using var operatorClient = _factory.CreateAuthenticatedClient();
        using var ingestClient = _factory.CreateIngestClient();

        var assetResponse = await operatorClient.PostAsJsonAsync("/api/assets", new CreateAssetRequest("Fleet Van 24", null, "Vehicle", AssetClass: "vehicle"));
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetDto>();

        var scheduleResponse = await operatorClient.PostAsJsonAsync("/api/maintenance/schedules", new CreateMaintenanceScheduleRequest(
            asset!.Id,
            "Mileage inspection",
            "inspection",
            IntervalOdometerKm: 1000,
            LastOdometerKm: 20000));
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<MaintenanceScheduleDto>();

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

        var completeResponse = await operatorClient.PostAsJsonAsync($"/api/maintenance/schedules/{schedule!.Id}/complete", new CompleteMaintenanceScheduleRequest(Notes: "Changed oil and inspected belts."));

        completeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await completeResponse.Content.ReadFromJsonAsync<MaintenanceServiceRecordDto>();
        record.Should().NotBeNull();
        record!.MaintenanceScheduleId.Should().Be(schedule.Id);
        record.OdometerKm.Should().Be(21000);
        record.Notes.Should().Be("Changed oil and inspected belts.");

        var schedules = await operatorClient.GetFromJsonAsync<List<MaintenanceScheduleDto>>($"/api/maintenance/schedules?assetId={asset.Id}");
        schedules.Should().ContainSingle();
        schedules![0].Status.Should().Be("current");
        schedules[0].LastOdometerKm.Should().Be(21000);
        schedules[0].NextOdometerKm.Should().Be(22000);

        var records = await operatorClient.GetFromJsonAsync<List<MaintenanceServiceRecordDto>>($"/api/maintenance/records?assetId={asset.Id}");
        records.Should().ContainSingle(x => x.Id == record.Id && x.ScheduleTitle == "Mileage inspection");
    }
}
