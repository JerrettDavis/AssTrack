namespace AssTrack.Domain.Contracts;

public sealed record GeofenceBreachDto(
    Guid Id,
    Guid ObservationId,
    Guid GeofenceId,
    string GeofenceName,
    Guid DeviceId,
    string? DeviceIdentifier,
    string? AssetName,
    Guid? AssetId,
    string EventType,
    DateTime DetectedAt,
    DateTime? AcknowledgedAtUtc,
    string? AcknowledgedBy);

public record AcknowledgeBreachRequest(string? AcknowledgedBy);
