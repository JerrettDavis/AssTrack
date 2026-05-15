namespace AssTrack.Domain.Models;

/// <summary>
/// In-memory job passed to <see cref="AssTrack.Api.Services.WebhookRetryWorker"/> via a bounded channel.
/// Loss on application restart is acceptable – retries are best-effort.
/// </summary>
public record WebhookRetryJob(
    Guid WebhookId,
    string Payload,
    string EventType,
    int AttemptNumber,
    string CorrelationId,
    DateTime ScheduledAt)
{
    public string? TargetUrl { get; init; }
    public string? SigningSecret { get; init; }
}
