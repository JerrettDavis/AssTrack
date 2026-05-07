namespace AssTrack.Domain.Contracts;

public record MessageThreadDto(
    Guid Id,
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    string? IntegrationFeedName,
    Guid? DeviceId,
    string? DeviceIdentifier,
    string? DeviceLabel,
    Guid? AssetId,
    string? AssetName,
    string? ExternalPeerId,
    string? DisplayName,
    string? Subject,
    string Status,
    string? Metadata,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastMessageAt,
    MessageEntryDto? LastMessage);

public record MessageEntryDto(
    Guid Id,
    Guid ThreadId,
    string Direction,
    string Status,
    string? Sender,
    string? Recipient,
    string Body,
    string? ProviderMessageId,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    DateTime CreatedAt,
    string? ErrorMessage,
    string? Metadata);

public record CreateMessageThreadRequest(
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    Guid? DeviceId,
    Guid? AssetId,
    string? ExternalPeerId,
    string? DisplayName,
    string? Subject,
    string? Metadata);

public record SendMessageRequest(string Body, string? Recipient, string? Metadata);

public record InboundMessageRequest(
    string Channel,
    string Provider,
    Guid? IntegrationFeedId,
    Guid? DeviceId,
    Guid? AssetId,
    string ExternalPeerId,
    string? DisplayName,
    string? Sender,
    string Body,
    string? ProviderMessageId,
    DateTime? ReceivedAt,
    string? Metadata);

public record OutboundMessageDto(
    Guid Id,
    Guid ThreadId,
    Guid? IntegrationFeedId,
    string Channel,
    string Provider,
    string? ExternalPeerId,
    string? DisplayName,
    string? Recipient,
    string Body,
    string? Metadata,
    DateTime CreatedAt);

public record UpdateMessageStatusRequest(
    string Status,
    string? ProviderMessageId,
    DateTime? SentAt,
    string? ErrorMessage);
