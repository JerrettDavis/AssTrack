namespace AssTrack.Domain.Contracts;

public sealed record ObservationDto(
    Guid Id,
    Guid DeviceId,
    string DeviceIdentifier,
    Guid? AssetId,
    string? AssetName,
    DateTime ObservedAt,
    DateTime ReceivedAt,
    double Latitude,
    double Longitude,
    double? Altitude,
    double? AccuracyMeters,
    double? SpeedKmh,
    double? HeadingDegrees,
    string? Metadata);

public sealed record CreateObservationRequest(
    Guid DeviceId,
    DateTime ObservedAt,
    double Latitude,
    double Longitude,
    double? Altitude,
    double? AccuracyMeters,
    double? SpeedKmh,
    double? HeadingDegrees,
    string? Metadata,
    string? DeviceIdentifier = null);

public sealed record ObservationTimelineDto(
    DateTime From,
    DateTime To,
    int BucketMinutes,
    int TotalCount,
    bool Truncated,
    IReadOnlyList<ObservationTimelineBucketDto> Buckets,
    IReadOnlyList<ObservationDto> Observations);

public sealed record ObservationTimelineBucketDto(
    DateTime Start,
    DateTime End,
    int Count);
