namespace AssTrack.Domain.Contracts;

/// <summary>
/// Webhook payload delivered when a speed alert is created.
/// </summary>
public sealed record SpeedAlertWebhookPayload(
    string EventType,
    Guid AlertId,
    Guid DeviceId,
    string? DeviceIdentifier,
    Guid? AssetId,
    string? AssetName,
    double ObservedSpeedKmh,
    double ThresholdKmh,
    DateTime TriggeredAt,
    DateTime DeliveredAt);

/// <summary>
/// Webhook payload delivered when a geofence breach (enter or exit) is created.
/// </summary>
public sealed record GeofenceBreachWebhookPayload(
    string EventType,
    Guid BreachId,
    Guid DeviceId,
    string? DeviceIdentifier,
    Guid? AssetId,
    string? AssetName,
    Guid GeofenceId,
    string GeofenceName,
    string BreachEventType,
    DateTime DetectedAt,
    DateTime DeliveredAt);

/// <summary>
/// Webhook payload delivered when an enterprise integration signal is published.
/// </summary>
public sealed record IntegrationEventWebhookPayload(
    string EventType,
    Guid EventId,
    string Source,
    string SignalType,
    string Severity,
    string? SubjectType,
    string? SubjectId,
    string? SubjectName,
    string Message,
    string? PayloadJson,
    DateTime OccurredAt,
    DateTime DeliveredAt,
    string? CorrelationId);
