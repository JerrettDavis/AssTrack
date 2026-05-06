using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public sealed class NormalizedJsonAdapter : ProviderPayloadAdapterBase
{
    public override string Provider => "generic-webhook";

    public override IReadOnlySet<string> Aliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "generic-webhook",
        "gps-http",
        "apple-findmy",
        "google-findhub",
        "samsung-find"
    };

    public override ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken)
    {
        var items = EnumeratePayload(payload).Select(item => ParseOne(context, item)).ToList();
        return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>(items);
    }

    private static IEnumerable<JsonElement> EnumeratePayload(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        var observations = Find(payload, "observations", "locations", "items");
        if (observations is { ValueKind: JsonValueKind.Array })
        {
            foreach (var item in observations.Value.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        yield return payload;
    }

    private static ProviderObservation ParseOne(BridgeFeedContext context, JsonElement item)
    {
        var provider = context.Provider;
        return RequiredObservation(
            context,
            item,
            provider,
            String(item, "externalTrackerId", "externalId", "trackerId", "deviceId", "id", "imei", "serial"),
            DateTimeUtc(item, "observedAt", "timestamp", "time", "createdAt", "date"),
            Double(item, "latitude", "lat"),
            Double(item, "longitude", "lon", "lng"),
            Double(item, "altitude", "alt"),
            Double(item, "accuracyMeters", "accuracy", "acc"),
            Double(item, "speedKmh", "speed", "speedKph"),
            Double(item, "headingDegrees", "heading", "bearing", "course"),
            String(item, "label", "name", "deviceName"),
            GuidValue(item, "assetId"),
            String(item, "tags"),
            new Dictionary<string, object?> { ["adapter"] = "normalized-json" });
    }
}
