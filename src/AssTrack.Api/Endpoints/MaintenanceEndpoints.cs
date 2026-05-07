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

        maintenance.MapGet("/reminders", async (
            Guid? assetId,
            MaintenanceScheduleRepository repository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var schedules = await repository.GetAllAsync(assetId, cancellationToken);
            var latestReadings = await GetLatestReadingsAsync(schedules, sensorRepository, cancellationToken);
            return Results.Ok(schedules
                .Select(schedule => Map(schedule, latestReadings))
                .Where(schedule => schedule.Status is MaintenanceStatus.Upcoming or MaintenanceStatus.Due or MaintenanceStatus.Overdue)
                .Select(MapReminder));
        }).RequireAuthorization("Operator");

        maintenance.MapPost("/schedules", async (
            CreateMaintenanceScheduleRequest request,
            MaintenanceScheduleRepository repository,
            AssetRepository assetRepository,
            SensorReadingRepository sensorRepository,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.AssetId, request.Title, request.ServiceType, request.IntervalDays, request.IntervalOdometerKm, request.IntervalRuntimeHours, request.DiagnosticSensorType, request.LastOdometerKm, request.LastRuntimeHours, assetRepository, cancellationToken);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var diagnosticSensorType = NormalizeSensorType(request.DiagnosticSensorType);
            var now = DateTime.UtcNow;
            var schedule = new MaintenanceSchedule
            {
                AssetId = request.AssetId,
                Title = request.Title.Trim(),
                ServiceType = NormalizeServiceType(request.ServiceType)!,
                IntervalDays = request.IntervalDays,
                IntervalOdometerKm = request.IntervalOdometerKm,
                IntervalRuntimeHours = request.IntervalRuntimeHours,
                DiagnosticSensorType = diagnosticSensorType,
                DiagnosticTextContains = string.IsNullOrWhiteSpace(request.DiagnosticTextContains) ? null : request.DiagnosticTextContains.Trim(),
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
            var validation = await ValidateAsync(request.AssetId, request.Title, request.ServiceType, request.IntervalDays, request.IntervalOdometerKm, request.IntervalRuntimeHours, request.DiagnosticSensorType, request.LastOdometerKm, request.LastRuntimeHours, assetRepository, cancellationToken);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var diagnosticSensorType = NormalizeSensorType(request.DiagnosticSensorType);
            var updated = await repository.UpdateAsync(new MaintenanceSchedule
            {
                Id = id,
                AssetId = request.AssetId,
                Title = request.Title.Trim(),
                ServiceType = NormalizeServiceType(request.ServiceType)!,
                IntervalDays = request.IntervalDays,
                IntervalOdometerKm = request.IntervalOdometerKm,
                IntervalRuntimeHours = request.IntervalRuntimeHours,
                DiagnosticSensorType = diagnosticSensorType,
                DiagnosticTextContains = string.IsNullOrWhiteSpace(request.DiagnosticTextContains) ? null : request.DiagnosticTextContains.Trim(),
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
        AssetDiagnosticReading? diagnostic = null;
        readings?.LatestDiagnostics.TryGetValue(schedule.Id, out diagnostic);
        var status = CalculateStatus(schedule, nextDueAt, nextOdometerKm, nextRuntimeHours, readings, diagnostic);

        return new MaintenanceScheduleDto(
            schedule.Id,
            schedule.AssetId,
            schedule.Asset?.Name,
            schedule.Title,
            schedule.ServiceType,
            schedule.IntervalDays,
            schedule.IntervalOdometerKm,
            schedule.IntervalRuntimeHours,
            schedule.DiagnosticSensorType,
            schedule.DiagnosticTextContains,
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
            readings?.LatestRuntimeHours,
            ApiDateTime.Utc(diagnostic?.ObservedAt),
            diagnostic?.Value);
    }

    internal static MaintenanceReminderDto MapReminder(MaintenanceScheduleDto schedule) => new(
        schedule.Id,
        schedule.AssetId,
        schedule.AssetName,
        schedule.Title,
        schedule.ServiceType,
        schedule.Status,
        ReminderReason(schedule),
        schedule.NextDueAt,
        schedule.LatestDiagnosticAt,
        schedule.LatestDiagnosticValue);

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

    private static string CalculateStatus(MaintenanceSchedule schedule, DateTime? nextDueAt, double? nextOdometerKm, double? nextRuntimeHours, AssetMaintenanceReadings? readings, AssetDiagnosticReading? diagnostic)
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
            IsMetricDue(readings?.LatestRuntimeHours, nextRuntimeHours) ||
            IsDiagnosticDue(schedule, diagnostic?.ObservedAt);
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

    private static bool IsDiagnosticDue(MaintenanceSchedule schedule, DateTime? diagnosticAt)
        => diagnosticAt is DateTime observedAt &&
            (!schedule.LastServiceAt.HasValue || observedAt > schedule.LastServiceAt.Value);

    private static string ReminderReason(MaintenanceScheduleDto schedule)
    {
        if (schedule.LatestDiagnosticAt.HasValue) return "Diagnostic event";
        if (schedule.NextDueAt.HasValue) return "Scheduled service date";
        if (schedule.NextOdometerKm.HasValue) return "Odometer interval";
        if (schedule.NextRuntimeHours.HasValue) return "Runtime interval";
        return "Maintenance schedule";
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
            var latestDiagnostics = new Dictionary<Guid, AssetDiagnosticReading>();
            foreach (var schedule in schedules.Where(x => x.AssetId == assetId && !string.IsNullOrWhiteSpace(x.DiagnosticSensorType)))
            {
                var reading = readings.FirstOrDefault(candidate => MatchesDiagnostic(candidate, schedule));
                if (reading is not null)
                {
                    latestDiagnostics[schedule.Id] = new AssetDiagnosticReading(reading.ObservedAt, DiagnosticValue(reading));
                }
            }
            result[assetId] = new AssetMaintenanceReadings(
                readings.FirstOrDefault(x => x.SensorType == SensorTypes.Odometer || x.SensorType == "odometer_km")?.NumericValue,
                readings.FirstOrDefault(x => x.SensorType == SensorTypes.Runtime || x.SensorType == "runtime_hours")?.NumericValue,
                latestDiagnostics);
        }

        return result;
    }

    private static bool MatchesDiagnostic(SensorReading reading, MaintenanceSchedule schedule)
    {
        if (!string.Equals(reading.SensorType, schedule.DiagnosticSensorType, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(schedule.DiagnosticTextContains)) return true;
        return (reading.TextValue ?? reading.Name ?? reading.Metadata ?? string.Empty)
            .Contains(schedule.DiagnosticTextContains.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? DiagnosticValue(SensorReading? reading)
    {
        if (reading is null) return null;
        if (!string.IsNullOrWhiteSpace(reading.TextValue)) return reading.TextValue;
        if (reading.NumericValue.HasValue) return reading.Unit is null ? reading.NumericValue.Value.ToString("G") : $"{reading.NumericValue.Value:G} {reading.Unit}";
        return reading.Name;
    }

    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        Guid assetId,
        string title,
        string? serviceType,
        int? intervalDays,
        double? intervalOdometerKm,
        double? intervalRuntimeHours,
        string? diagnosticSensorType,
        double? lastOdometerKm,
        double? lastRuntimeHours,
        AssetRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (await assetRepository.GetByIdAsync(assetId, cancellationToken) is null) errors["assetId"] = ["Asset is required."];
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Title is required."];
        if (NormalizeServiceType(serviceType) is null) errors["serviceType"] = ["Service type is not supported."];
        if (!intervalDays.HasValue && !intervalOdometerKm.HasValue && !intervalRuntimeHours.HasValue && string.IsNullOrWhiteSpace(diagnosticSensorType)) errors["interval"] = ["At least one interval or diagnostic trigger is required."];
        if (intervalDays.HasValue && intervalDays.Value <= 0) errors["intervalDays"] = ["Interval days must be positive."];
        if (!IsPositive(intervalOdometerKm)) errors["intervalOdometerKm"] = ["Odometer interval must be positive."];
        if (!IsPositive(intervalRuntimeHours)) errors["intervalRuntimeHours"] = ["Runtime interval must be positive."];
        if (diagnosticSensorType is not null && NormalizeSensorType(diagnosticSensorType) is null) errors["diagnosticSensorType"] = ["Diagnostic sensor type is not supported."];
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

    private static string? NormalizeSensorType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized.Length <= 80 ? normalized : null;
    }

    private static bool IsPositive(double? value)
        => !value.HasValue || (!double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0);

    private static bool IsNonNegative(double? value)
        => !value.HasValue || (!double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value >= 0);

    internal sealed record AssetMaintenanceReadings(double? LatestOdometerKm, double? LatestRuntimeHours, IReadOnlyDictionary<Guid, AssetDiagnosticReading> LatestDiagnostics);

    internal sealed record AssetDiagnosticReading(DateTime ObservedAt, string? Value);
}
