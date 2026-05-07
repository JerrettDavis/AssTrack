namespace AssTrack.BridgeGateway.Services;

public interface IAssTrackIngestClient
{
    Task<BridgeDeliveryResult> SendAsync(Guid feedId, ProviderObservation observation, CancellationToken cancellationToken);
    Task<BridgeDeliveryResult> SendDeviceProfileAsync(Guid feedId, ProviderDeviceProfile profile, CancellationToken cancellationToken);
}

public sealed record BridgeDeliveryResult(bool Success, int StatusCode, string? ResponseBody, bool Retryable);
