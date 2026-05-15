namespace AssTrack.Domain.Contracts;

public sealed record AuditEventDto(
    Guid Id,
    DateTime OccurredAt,
    string ActorName,
    string ActorRole,
    string Action,
    string EntityType,
    string? EntityId,
    string? EntityName,
    string? Summary,
    string? MetadataJson,
    string? CorrelationId);
