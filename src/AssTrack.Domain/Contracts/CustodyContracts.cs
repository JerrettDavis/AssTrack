namespace AssTrack.Domain.Contracts;

public sealed record CustodyEventDto(
    Guid Id,
    Guid AssetId,
    string? AssetName,
    string EventType,
    string? FromCustodianName,
    string? ToCustodianName,
    string? ToCustodianContact,
    string? Location,
    string? Notes,
    DateTime OccurredAt,
    DateTime CreatedAt);

public sealed record CreateCustodyEventRequest(
    Guid AssetId,
    string EventType,
    string? ToCustodianName = null,
    string? ToCustodianContact = null,
    string? CustodyStatus = null,
    string? Location = null,
    string? Notes = null,
    DateTime? OccurredAt = null);
