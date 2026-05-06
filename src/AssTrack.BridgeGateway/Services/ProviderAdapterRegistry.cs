using AssTrack.BridgeGateway.Adapters;

namespace AssTrack.BridgeGateway.Services;

public sealed class ProviderAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IProviderPayloadAdapter> _adapters;

    public ProviderAdapterRegistry(IEnumerable<IProviderPayloadAdapter> adapters)
    {
        var map = new Dictionary<string, IProviderPayloadAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in adapters)
        {
            map[adapter.Provider] = adapter;
            foreach (var alias in adapter.Aliases)
            {
                map[alias] = adapter;
            }
        }

        _adapters = map;
    }

    public IReadOnlyList<BridgeProviderDescriptor> Providers => _adapters
        .GroupBy(item => item.Value.Provider, StringComparer.OrdinalIgnoreCase)
        .Select(group => new BridgeProviderDescriptor(group.Key, group.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray()))
        .OrderBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IProviderPayloadAdapter? Get(string provider)
        => _adapters.TryGetValue(provider, out var adapter) ? adapter : null;
}

public sealed record BridgeProviderDescriptor(string Provider, IReadOnlyList<string> Aliases);
