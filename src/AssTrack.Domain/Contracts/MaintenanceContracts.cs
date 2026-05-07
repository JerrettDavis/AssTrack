namespace AssTrack.Domain.Contracts;

public sealed record MaintenanceScheduleDto(
    Guid Id,
    Guid AssetId,
    string? AssetName,
    string Title,
    string ServiceType,
    int? IntervalDays,
    double? IntervalOdometerKm,
    double? IntervalRuntimeHours,
    DateTime? LastServiceAt,
    double? LastOdometerKm,
    double? LastRuntimeHours,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Status,
    DateTime? NextDueAt,
    double? NextOdometerKm,
    double? NextRuntimeHours,
    double? LatestOdometerKm,
    double? LatestRuntimeHours);

public sealed record MaintenanceServiceRecordDto(
    Guid Id,
    Guid MaintenanceScheduleId,
    Guid AssetId,
    string? AssetName,
    string ScheduleTitle,
    string ServiceType,
    DateTime CompletedAt,
    double? OdometerKm,
    double? RuntimeHours,
    string? PerformedBy,
    decimal? Cost,
    string? Notes,
    DateTime CreatedAt);

public sealed record CreateMaintenanceScheduleRequest(
    Guid AssetId,
    string Title,
    string? ServiceType = null,
    int? IntervalDays = null,
    double? IntervalOdometerKm = null,
    double? IntervalRuntimeHours = null,
    DateTime? LastServiceAt = null,
    double? LastOdometerKm = null,
    double? LastRuntimeHours = null,
    string? Notes = null);

public sealed record UpdateMaintenanceScheduleRequest(
    Guid AssetId,
    string Title,
    string? ServiceType = null,
    int? IntervalDays = null,
    double? IntervalOdometerKm = null,
    double? IntervalRuntimeHours = null,
    DateTime? LastServiceAt = null,
    double? LastOdometerKm = null,
    double? LastRuntimeHours = null,
    string? Notes = null);

public sealed record CompleteMaintenanceScheduleRequest(
    DateTime? CompletedAt = null,
    double? OdometerKm = null,
    double? RuntimeHours = null,
    string? PerformedBy = null,
    decimal? Cost = null,
    string? Notes = null);
