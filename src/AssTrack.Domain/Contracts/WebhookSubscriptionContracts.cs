namespace AssTrack.Domain.Contracts;

public sealed record WebhookSubscriptionDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    string EventTypes,
    string TargetUrl,
    bool SigningEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastAttemptedAt = null,
    DateTime? LastSuccessAt = null,
    DateTime? LastFailureAt = null,
    int? LastHttpStatusCode = null,
    string? LastErrorMessage = null,
    int Last24hDeliveries = 0,
    int Last24hFailures = 0,
    string Health = "idle");

public sealed record CreateWebhookSubscriptionRequest(
    string Name,
    bool IsEnabled,
    string EventTypes,
    string TargetUrl,
    string? SigningSecret);

public sealed record UpdateWebhookSubscriptionRequest(
    string Name,
    bool IsEnabled,
    string EventTypes,
    string TargetUrl,
    string? SigningSecret);
