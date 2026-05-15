namespace AssTrack.Domain.Contracts;

public sealed record IntegrationEventDto(
    Guid Id,
    DateTime OccurredAt,
    string Source,
    string? ExternalEventId,
    string EventType,
    string Severity,
    string? SubjectType,
    string? SubjectId,
    string? SubjectName,
    string Message,
    string? PayloadJson,
    string? CorrelationId,
    string Status,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    string? ResolutionNote);

public sealed record CreateIntegrationEventRequest(
    string Source,
    string? ExternalEventId,
    string EventType,
    string Severity,
    string? SubjectType,
    string? SubjectId,
    string? SubjectName,
    string Message,
    object? Payload,
    DateTime? OccurredAt);

public sealed record ResolveIntegrationEventRequest(string? ResolutionNote);
