using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public sealed class OwnTracksAdapter : ProviderPayloadAdapterBase
{
    public override string Provider => "owntracks";
    public override IReadOnlySet<string> Aliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "owntracks" };

    public override ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var observations = payload.EnumerateArray()
                .Where(item => string.Equals(String(item, "_type"), "location", StringComparison.OrdinalIgnoreCase))
                .Select(item => ParseOne(context, item))
                .ToList();
            return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>(observations);
        }

        var type = String(payload, "_type");
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "location", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>([ParseOne(context, payload)]);
    }

    private static ProviderObservation ParseOne(BridgeFeedContext context, JsonElement payload)
    {
        var topic = String(payload, "topic");
        var user = String(payload, "u", "user", "username");
        var device = String(payload, "d", "device", "deviceName");
        var trackerId = String(payload, "tid");
        var externalId = FirstNonBlank(topic, JoinId(user, device), trackerId);

        return RequiredObservation(
            context,
            payload,
            "owntracks",
            externalId,
            DateTimeUtc(payload, "tst", "created_at", "observedAt"),
            Double(payload, "lat"),
            Double(payload, "lon"),
            Double(payload, "alt"),
            Double(payload, "acc"),
            SpeedToKmh(Double(payload, "vel")),
            Double(payload, "cog"),
            FirstNonBlank(device, trackerId, topic),
            GuidValue(payload, "assetId"),
            "owntracks, phone",
            new Dictionary<string, object?>
            {
                ["adapter"] = "owntracks",
                ["topic"] = topic,
                ["battery"] = Double(payload, "batt", "B")
            });
    }

    private static string? JoinId(string? user, string? device)
        => string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(device) ? null : $"{user}/{device}";

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static double? SpeedToKmh(double? ownTracksVelocity)
        => ownTracksVelocity is null ? null : ownTracksVelocity.Value;
}
