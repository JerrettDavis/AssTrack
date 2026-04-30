namespace AssTrack.Domain.Contracts;

/// <summary>DTO returned from GET /api/webhooks/deliveries.</summary>
public sealed record WebhookDeliveryLogDto(
    int Id,
    DateTime AttemptedAt,
    string EventType,
    string TargetUrl,
    bool Success,
    int? HttpStatusCode,
    int DurationMs,
    string? ErrorMessage,
    string? RequestPayloadSummary);

/// <summary>DTO returned from GET /api/webhooks/status.</summary>
public sealed record WebhookStatusDto(
    bool Configured,
    int Last24hDeliveries,
    int Last24hFailures,
    DateTime? LastDeliveredAt,
    double? AvgDurationMs);
