namespace AssTrack.Domain.Contracts;

public sealed record AlertRoutingRuleDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    string EventType,
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    string? IntegrationFeedName,
    string? ExternalPeerId,
    string? DisplayName,
    string? Recipient,
    string? MessageTemplate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateAlertRoutingRuleRequest(
    string Name,
    bool IsEnabled,
    string EventType,
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    string? ExternalPeerId,
    string? DisplayName,
    string? Recipient,
    string? MessageTemplate);

public sealed record UpdateAlertRoutingRuleRequest(
    string Name,
    bool IsEnabled,
    string EventType,
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    string? ExternalPeerId,
    string? DisplayName,
    string? Recipient,
    string? MessageTemplate);
