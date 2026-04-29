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
    string? Metadata);
