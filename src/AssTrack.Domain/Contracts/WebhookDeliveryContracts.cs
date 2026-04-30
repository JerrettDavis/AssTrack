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
    string? RequestPayloadSummary,
    int AttemptNumber,
    string CorrelationId);

/// <summary>DTO returned from GET /api/webhooks/status.</summary>
public sealed record WebhookStatusDto(
    bool Configured,
    int Last24hDeliveries,
    int Last24hFailures,
    DateTime? LastDeliveredAt,
    double? AvgDurationMs,
    int RetryQueueDepth = 0,
    bool SigningEnabled = false);

/// <summary>Request body for POST /api/webhooks/test.</summary>
public sealed record TestWebhookRequest(string? EventType);

/// <summary>Response from POST /api/webhooks/test.</summary>
public sealed record TestWebhookFireResponse(
    bool Fired,
    string EventType,
    bool Configured,
    string Message);

