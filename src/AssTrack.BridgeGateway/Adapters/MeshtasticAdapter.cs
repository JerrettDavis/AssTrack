using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public sealed class MeshtasticAdapter : ProviderPayloadAdapterBase
{
    public override string Provider => "meshtastic";
    public override IReadOnlySet<string> Aliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "meshtastic" };

    public override ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken)
    {
        var packet = Find(payload, "packet") is { ValueKind: JsonValueKind.Object } packetElement ? packetElement : payload;
        var decoded = Find(packet, "decoded") is { ValueKind: JsonValueKind.Object } decodedElement ? decodedElement : packet;
        var envelopePayload = Find(payload, "payload") is { ValueKind: JsonValueKind.Object } payloadElement ? payloadElement : decoded;
        var position = Find(decoded, "position") is { ValueKind: JsonValueKind.Object } positionElement
            ? positionElement
            : Find(envelopePayload, "position") is { ValueKind: JsonValueKind.Object } payloadPositionElement
                ? payloadPositionElement
                : envelopePayload;

        var externalId = FirstNonBlank(
            String(payload, "sender"),
            String(envelopePayload, "id", "nodeId"),
            String(payload, "fromId", "from", "id"),
            String(packet, "fromId", "from"),
            String(payload, "nodeId"),
            String(position, "nodeId"));

        var label = FirstNonBlank(
            String(payload, "longName", "longname", "shortName", "shortname", "name"),
            String(packet, "longName", "longname", "shortName", "shortname", "name"),
            String(envelopePayload, "longName", "longname", "shortName", "shortname", "name"));

        var latitude = Double(position, "latitude", "lat");
        var longitude = Double(position, "longitude", "lon", "lng");

        latitude ??= ScaledCoordinate(Double(position, "latitudeI", "latitude_i", "latitudeIi"));
        longitude ??= ScaledCoordinate(Double(position, "longitudeI", "longitude_i", "longitudeIi"));

        var observation = RequiredObservation(
            context,
            payload,
            "meshtastic",
            externalId,
            DateTimeUtc(position, "time", "timestamp", "observedAt") ?? DateTimeUtc(payload, "timestamp", "rxTime"),
            latitude,
            longitude,
            Double(position, "altitude", "altitudeMeters", "alt"),
            Double(position, "precisionBits", "accuracyMeters"),
            SpeedMetersPerSecondToKmh(Double(position, "groundSpeed", "speed")),
            Double(position, "groundTrack", "heading", "headingDegrees"),
            label,
            GuidValue(payload, "assetId"),
            "meshtastic, lora",
            new Dictionary<string, object?>
            {
                ["adapter"] = "meshtastic",
                ["messageType"] = String(payload, "type"),
                ["channel"] = String(payload, "channel"),
                ["topic"] = String(payload, "topic")
            });

        return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>([observation]);
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static double? ScaledCoordinate(double? integerCoordinate)
        => integerCoordinate is null ? null : integerCoordinate.Value / 10_000_000d;

    private static double? SpeedMetersPerSecondToKmh(double? metersPerSecond)
        => metersPerSecond is null ? null : Math.Round(metersPerSecond.Value * 3.6, 3);
}
