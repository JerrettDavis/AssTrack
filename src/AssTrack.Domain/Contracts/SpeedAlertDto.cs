namespace AssTrack.Domain.Contracts;

public sealed record SpeedAlertDto(
    Guid Id,
    Guid ObservationId,
    Guid DeviceId,
    Guid? AssetId,
    double ObservedSpeedKmh,
    double ThresholdKmh,
    DateTime TriggeredAt,
    string? DeviceIdentifier,
    string? AssetName);
