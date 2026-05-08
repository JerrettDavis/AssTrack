namespace AssTrack.BridgeGateway.Services;

using AssTrack.Domain.Contracts;

public interface IAssTrackIngestClient
{
    Task<BridgeDeliveryResult> SendAsync(Guid feedId, ProviderObservation observation, CancellationToken cancellationToken);
    Task<BridgeDeliveryResult> SendDeviceProfileAsync(Guid feedId, ProviderDeviceProfile profile, CancellationToken cancellationToken);
    Task<BridgeDeliveryResult> SendInboundMessageAsync(InboundMessageRequest message, CancellationToken cancellationToken);
    Task<BridgeOutboundMessagesResult> GetOutboundMessagesAsync(Guid feedId, int take, CancellationToken cancellationToken);
    Task<BridgeDeliveryResult> UpdateMessageStatusAsync(Guid messageId, UpdateMessageStatusRequest status, CancellationToken cancellationToken);
}

public sealed record BridgeDeliveryResult(bool Success, int StatusCode, string? ResponseBody, bool Retryable);

public sealed record BridgeOutboundMessagesResult(
    bool Success,
    int StatusCode,
    string? ResponseBody,
    bool Retryable,
    IReadOnlyList<OutboundMessageDto> Messages);
