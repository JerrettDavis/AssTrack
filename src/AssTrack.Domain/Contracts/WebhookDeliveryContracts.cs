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
    bool SigningEnabled = false,
    int EnabledSubscriptions = 0);

/// <summary>Request body for POST /api/webhooks/test.</summary>
public sealed record TestWebhookRequest(string? EventType);

/// <summary>Response from POST /api/webhooks/test.</summary>
public sealed record TestWebhookFireResponse(
    bool Fired,
    string EventType,
    bool Configured,
    string Message);

/// <summary>Response from POST /api/webhooks/deliveries/{id}/replay.</summary>
public sealed record WebhookReplayResponse(
    bool Replayed,
    int SourceDeliveryId,
    string EventType,
    string TargetUrl,
    string Message);

/// <summary>Response from POST /api/webhooks/subscriptions/{id}/test.</summary>
public sealed record WebhookSubscriptionTestResponse(
    bool Fired,
    Guid SubscriptionId,
    string EventType,
    string TargetUrl,
    string Message);
