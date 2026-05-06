using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public interface IProviderPayloadAdapter
{
    string Provider { get; }
    IReadOnlySet<string> Aliases { get; }
    ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken);
}
