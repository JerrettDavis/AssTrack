using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public sealed class TraccarAdapter : ProviderPayloadAdapterBase
{
    public override string Provider => "traccar";
    public override IReadOnlySet<string> Aliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "traccar" };

    public override ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken)
    {
        var position = Find(payload, "position") is { ValueKind: JsonValueKind.Object } p ? p : payload;
        var device = Find(payload, "device") is { ValueKind: JsonValueKind.Object } d ? d : default(JsonElement?);
        var deviceId = device is null
            ? String(position, "deviceId", "uniqueId", "externalTrackerId", "id")
            : FirstNonBlank(String(device.Value, "uniqueId", "id"), String(position, "deviceId"));
        var label = device is null ? String(position, "name", "label") : String(device.Value, "name", "label");

        var observation = RequiredObservation(
            context,
            position,
            "traccar",
            deviceId,
            DateTimeUtc(position, "fixTime", "deviceTime", "serverTime", "observedAt"),
            Double(position, "latitude", "lat"),
            Double(position, "longitude", "lon"),
            Double(position, "altitude", "alt"),
            Double(position, "accuracy", "accuracyMeters"),
            KnotsToKmh(Double(position, "speed")),
            Double(position, "course", "heading", "headingDegrees"),
            label,
            GuidValue(payload, "assetId"),
            "traccar, gps",
            new Dictionary<string, object?>
            {
                ["adapter"] = "traccar",
                ["eventType"] = String(payload, "type"),
                ["positionId"] = String(position, "id")
            });

        return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>([observation]);
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static double? KnotsToKmh(double? knots)
        => knots is null ? null : Math.Round(knots.Value * 1.852, 3);
}
