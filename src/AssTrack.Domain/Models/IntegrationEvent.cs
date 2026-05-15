namespace AssTrack.Domain.Models;

public class IntegrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string? ExternalEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = IntegrationEventSeverities.Info;
    public string? SubjectType { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = IntegrationEventStatuses.Open;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNote { get; set; }
}

public static class IntegrationEventTypes
{
    public const string EnterpriseSignal = "enterprise_signal";
}

public static class IntegrationEventSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public static class IntegrationEventStatuses
{
    public const string Open = "open";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";
}
