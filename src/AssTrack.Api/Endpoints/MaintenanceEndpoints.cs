using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using AssTrack.Api;

namespace AssTrack.Api.Endpoints;

public static class MaintenanceEndpoints
{
    public static RouteGroupBuilder MapMaintenanceEndpoints(this RouteGroupBuilder group)
    {
        var maintenance = group.MapGroup("/maintenance");

        maintenance.MapGet("/schedules", async (
            Guid? assetId,
            MaintenanceScheduleRepository repository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var schedules = await repository.GetAllAsync(assetId, cancellationToken);
            var latestReadings = await GetLatestReadingsAsync(schedules, sensorRepository, cancellationToken);
            return Results.Ok(schedules.Select(schedule => Map(schedule, latestReadings)));
        }).RequireAuthorization("Operator");

        maintenance.MapGet("/schedules/{id:guid}", async (
            Guid id,
            MaintenanceScheduleRepository repository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var schedule = await repository.GetByIdAsync(id, cancellationToken);
            if (schedule is null) return Results.NotFound();

            var latestReadings = await GetLatestReadingsAsync([schedule], sensorRepository, cancellationToken);
            return Results.Ok(Map(schedule, latestReadings));
        }).RequireAuthorization("Operator");

        maintenance.MapGet("/records", async (
            Guid? scheduleId,
            Guid? assetId,
            int? limit,
            MaintenanceScheduleRepository repository,
            CancellationToken cancellationToken) =>
        {
            var records = await repository.GetServiceRecordsAsync(scheduleId, assetId, limit ?? 200, cancellationToken);
            return Results.Ok(records.Select(MapRecord));
        }).RequireAuthorization("Operator");

        maintenance.MapPost("/schedules", async (
            CreateMaintenanceScheduleRequest request,
            MaintenanceScheduleRepository repository,
            AssetRepository assetRepository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.AssetId, request.Title, request.ServiceType, request.IntervalDays, request.IntervalOdometerKm, request.IntervalRuntimeHours, request.LastOdometerKm, request.LastRuntimeHours, assetRepository, cancellationToken);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var now = DateTime.UtcNow;
            var schedule = new MaintenanceSchedule
            {
                AssetId = request.AssetId,
                Title = request.Title.Trim(),
                ServiceType = NormalizeServiceType(request.ServiceType)!,
                IntervalDays = request.IntervalDays,
                IntervalOdometerKm = request.IntervalOdometerKm,
                IntervalRuntimeHours = request.IntervalRuntimeHours,
                LastServiceAt = request.LastServiceAt,
                LastOdometerKm = request.LastOdometerKm,
                LastRuntimeHours = request.LastRuntimeHours,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            var created = await repository.AddAsync(schedule, cancellationToken);
            var latestReadings = await GetLatestReadingsAsync([created], sensorRepository, cancellationToken);
            return Results.Created($"/api/maintenance/schedules/{created.Id}", Map(created, latestReadings));
        }).RequireAuthorization("Operator");

        maintenance.MapPut("/schedules/{id:guid}", async (
            Guid id,
            UpdateMaintenanceScheduleRequest request,
            MaintenanceScheduleRepository repository,
            AssetRepository assetRepository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.AssetId, request.Title, request.ServiceType, request.IntervalDays, request.IntervalOdometerKm, request.IntervalRuntimeHours, request.LastOdometerKm, request.LastRuntimeHours, assetRepository, cancellationToken);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var updated = await repository.UpdateAsync(new MaintenanceSchedule
            {
                Id = id,
                AssetId = request.AssetId,
                Title = request.Title.Trim(),
                ServiceType = NormalizeServiceType(request.ServiceType)!,
                IntervalDays = request.IntervalDays,
                IntervalOdometerKm = request.IntervalOdometerKm,
                IntervalRuntimeHours = request.IntervalRuntimeHours,
                LastServiceAt = request.LastServiceAt,
                LastOdometerKm = request.LastOdometerKm,
                LastRuntimeHours = request.LastRuntimeHours,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            }, cancellationToken);
            if (updated is null) return Results.NotFound();

            var latestReadings = await GetLatestReadingsAsync([updated], sensorRepository, cancellationToken);
            return Results.Ok(Map(updated, latestReadings));
        }).RequireAuthorization("Operator");

        maintenance.MapDelete("/schedules/{id:guid}", async (Guid id, MaintenanceScheduleRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Operator");

        maintenance.MapPost("/schedules/{id:guid}/complete", async (
            Guid id,
            CompleteMaintenanceScheduleRequest request,
            MaintenanceScheduleRepository repository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var schedule = await repository.GetByIdAsync(id, cancellationToken);
            if (schedule is null) return Results.NotFound();

            var latestReadings = await GetLatestReadingsAsync([schedule], sensorRepository, cancellationToken);
            latestReadings.TryGetValue(schedule.AssetId, out var readings);
            var odometerKm = request.OdometerKm ?? readings?.LatestOdometerKm;
            var runtimeHours = request.RuntimeHours ?? readings?.LatestRuntimeHours;
            var validation = ValidateCompletion(request.CompletedAt, odometerKm, runtimeHours, request.Cost);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var record = await repository.AddServiceRecordAsync(
                id,
                request.CompletedAt ?? DateTime.UtcNow,
                odometerKm,
                runtimeHours,
                string.IsNullOrWhiteSpace(request.PerformedBy) ? null : request.PerformedBy.Trim(),
                request.Cost,
                string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                cancellationToken);

            return record is null ? Results.NotFound() : Results.Created($"/api/maintenance/records/{record.Id}", MapRecord(record));
        }).RequireAuthorization("Operator");

        return group;
    }

    internal static MaintenanceScheduleDto Map(MaintenanceSchedule schedule, IReadOnlyDictionary<Guid, AssetMaintenanceReadings> latestReadings)
    {
        latestReadings.TryGetValue(schedule.AssetId, out var readings);
        DateTime? nextDueAt = schedule.LastServiceAt.HasValue && schedule.IntervalDays.HasValue
            ? schedule.LastServiceAt.Value.AddDays(schedule.IntervalDays.Value)
            : null;
        var nextOdometerKm = AddNullable(schedule.LastOdometerKm, schedule.IntervalOdometerKm);
        var nextRuntimeHours = AddNullable(schedule.LastRuntimeHours, schedule.IntervalRuntimeHours);
        var status = CalculateStatus(schedule, nextDueAt, nextOdometerKm, nextRuntimeHours, readings);

        return new MaintenanceScheduleDto(
            schedule.Id,
            schedule.AssetId,
            schedule.Asset?.Name,
            schedule.Title,
            schedule.ServiceType,
            schedule.IntervalDays,
            schedule.IntervalOdometerKm,
            schedule.IntervalRuntimeHours,
            ApiDateTime.Utc(schedule.LastServiceAt),
            schedule.LastOdometerKm,
            schedule.LastRuntimeHours,
            schedule.Notes,
            ApiDateTime.Utc(schedule.CreatedAt),
            ApiDateTime.Utc(schedule.UpdatedAt),
            status,
            ApiDateTime.Utc(nextDueAt),
            nextOdometerKm,
            nextRuntimeHours,
            readings?.LatestOdometerKm,
            readings?.LatestRuntimeHours);
    }

    internal static MaintenanceServiceRecordDto MapRecord(MaintenanceServiceRecord record) => new(
        record.Id,
        record.MaintenanceScheduleId,
        record.AssetId,
        record.Asset?.Name,
        record.MaintenanceSchedule?.Title ?? string.Empty,
        record.MaintenanceSchedule?.ServiceType ?? MaintenanceServiceTypes.General,
        ApiDateTime.Utc(record.CompletedAt),
        record.OdometerKm,
        record.RuntimeHours,
        record.PerformedBy,
        record.Cost,
        record.Notes,
        ApiDateTime.Utc(record.CreatedAt));

    private static string CalculateStatus(MaintenanceSchedule schedule, DateTime? nextDueAt, double? nextOdometerKm, double? nextRuntimeHours, AssetMaintenanceReadings? readings)
    {
        var now = DateTime.UtcNow;
        var isOverdue =
            (nextDueAt.HasValue && now > nextDueAt.Value.AddDays(7)) ||
            IsMetricBeyondGrace(readings?.LatestOdometerKm, nextOdometerKm, schedule.IntervalOdometerKm, 100) ||
            IsMetricBeyondGrace(readings?.LatestRuntimeHours, nextRuntimeHours, schedule.IntervalRuntimeHours, 10);
        if (isOverdue) return MaintenanceStatus.Overdue;

        var isDue =
            (nextDueAt.HasValue && now >= nextDueAt.Value) ||
            IsMetricDue(readings?.LatestOdometerKm, nextOdometerKm) ||
            IsMetricDue(readings?.LatestRuntimeHours, nextRuntimeHours);
        if (isDue) return MaintenanceStatus.Due;

        var isUpcoming =
            (nextDueAt.HasValue && nextDueAt.Value <= now.AddDays(14)) ||
            IsMetricUpcoming(readings?.LatestOdometerKm, nextOdometerKm, 250) ||
            IsMetricUpcoming(readings?.LatestRuntimeHours, nextRuntimeHours, 10);

        return isUpcoming ? MaintenanceStatus.Upcoming : MaintenanceStatus.Current;
    }

    private static bool IsMetricDue(double? latest, double? nextDue)
        => latest.HasValue && nextDue.HasValue && latest.Value >= nextDue.Value;

    private static bool IsMetricUpcoming(double? latest, double? nextDue, double window)
        => latest.HasValue && nextDue.HasValue && latest.Value < nextDue.Value && latest.Value >= nextDue.Value - window;

    private static bool IsMetricBeyondGrace(double? latest, double? nextDue, double? interval, double minimumGrace)
    {
        if (!latest.HasValue || !nextDue.HasValue) return false;
        var grace = Math.Max(minimumGrace, (interval ?? 0) * 0.1);
        return latest.Value > nextDue.Value + grace;
    }

    private static double? AddNullable(double? baseline, double? interval)
        => baseline.HasValue && interval.HasValue ? baseline.Value + interval.Value : null;

    private static async Task<IReadOnlyDictionary<Guid, AssetMaintenanceReadings>> GetLatestReadingsAsync(
        IReadOnlyCollection<MaintenanceSchedule> schedules,
        SensorReadingRepository sensorRepository,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, AssetMaintenanceReadings>();
        foreach (var assetId in schedules.Select(x => x.AssetId).Distinct())
        {
            var readings = await sensorRepository.GetRecentAsync(assetId: assetId, limit: 200, cancellationToken: cancellationToken);
            result[assetId] = new AssetMaintenanceReadings(
                readings.FirstOrDefault(x => x.SensorType == SensorTypes.Odometer || x.SensorType == "odometer_km")?.NumericValue,
                readings.FirstOrDefault(x => x.SensorType == SensorTypes.Runtime || x.SensorType == "runtime_hours")?.NumericValue);
        }

        return result;
    }

    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        Guid assetId,
        string title,
        string? serviceType,
        int? intervalDays,
        double? intervalOdometerKm,
        double? intervalRuntimeHours,
        double? lastOdometerKm,
        double? lastRuntimeHours,
        AssetRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (await assetRepository.GetByIdAsync(assetId, cancellationToken) is null) errors["assetId"] = ["Asset is required."];
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Title is required."];
        if (NormalizeServiceType(serviceType) is null) errors["serviceType"] = ["Service type is not supported."];
        if (!intervalDays.HasValue && !intervalOdometerKm.HasValue && !intervalRuntimeHours.HasValue) errors["interval"] = ["At least one interval is required."];
        if (intervalDays.HasValue && intervalDays.Value <= 0) errors["intervalDays"] = ["Interval days must be positive."];
        if (!IsPositive(intervalOdometerKm)) errors["intervalOdometerKm"] = ["Odometer interval must be positive."];
        if (!IsPositive(intervalRuntimeHours)) errors["intervalRuntimeHours"] = ["Runtime interval must be positive."];
        if (!IsNonNegative(lastOdometerKm)) errors["lastOdometerKm"] = ["Last odometer must be zero or greater."];
        if (!IsNonNegative(lastRuntimeHours)) errors["lastRuntimeHours"] = ["Last runtime must be zero or greater."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateCompletion(DateTime? completedAt, double? odometerKm, double? runtimeHours, decimal? cost)
    {
        var errors = new Dictionary<string, string[]>();
        if (completedAt.HasValue && completedAt.Value > DateTime.UtcNow.AddMinutes(5)) errors["completedAt"] = ["Completed time cannot be in the future."];
        if (!IsNonNegative(odometerKm)) errors["odometerKm"] = ["Odometer must be zero or greater."];
        if (!IsNonNegative(runtimeHours)) errors["runtimeHours"] = ["Runtime must be zero or greater."];
        if (cost.HasValue && cost.Value < 0) errors["cost"] = ["Cost must be zero or greater."];
        return errors;
    }

    private static string? NormalizeServiceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return MaintenanceServiceTypes.General;
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return MaintenanceServiceTypes.All.Contains(normalized) ? normalized : null;
    }

    private static bool IsPositive(double? value)
        => !value.HasValue || (!double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0);

    private static bool IsNonNegative(double? value)
        => !value.HasValue || (!double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value >= 0);

    internal sealed record AssetMaintenanceReadings(double? LatestOdometerKm, double? LatestRuntimeHours);
}
