using AssTrack.Domain.Contracts;

namespace AssTrack.MessageBridge.Worker;

public interface IMessageProviderClient
{
    Task<IReadOnlyList<InboundProviderMessage>> ReceiveInboundMessagesAsync(CancellationToken cancellationToken);
    Task<ProviderSendResult> SendOutboundMessageAsync(OutboundMessageDto message, CancellationToken cancellationToken);
}

public sealed record InboundProviderMessage(
    string ExternalPeerId,
    string Body,
    string? Channel,
    string? DisplayName,
    string? Sender,
    string? ProviderMessageId,
    DateTime? ReceivedAt,
    Guid? DeviceId,
    Guid? AssetId,
    string? Metadata);

public sealed record ProviderSendResult(
    string Status,
    string? ProviderMessageId,
    DateTime? SentAt,
    string? ErrorMessage);
