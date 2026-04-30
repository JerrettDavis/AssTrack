namespace AssTrack.Domain.Contracts;

public sealed record GeofenceBreachDto(
    Guid Id,
    Guid ObservationId,
    Guid GeofenceId,
    string GeofenceName,
    Guid DeviceId,
    Guid? AssetId,
    DateTime DetectedAt);
