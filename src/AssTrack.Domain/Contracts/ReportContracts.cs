namespace AssTrack.Domain.Contracts;

public sealed record UtilizationReportDto(
    DateTime From,
    DateTime To,
    DateTime GeneratedAt,
    int AssetCount,
    int DeviceCount,
    int ObservationCount,
    double TotalDistanceKm,
    double TotalMovingMinutes,
    double TotalIdleMinutes,
    IReadOnlyList<UtilizationReportItemDto> Items);

public sealed record UtilizationReportItemDto(
    Guid DeviceId,
    string DeviceIdentifier,
    Guid? AssetId,
    string? AssetName,
    DateTime? FirstObservedAt,
    DateTime? LastObservedAt,
    int ObservationCount,
    double DistanceKm,
    double MovingMinutes,
    double IdleMinutes,
    int StopCount,
    double? MaxSpeedKmh,
    double? AverageMovingSpeedKmh);
