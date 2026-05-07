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
            NodeId(packet, "fromId", "from"),
            NodeId(payload, "fromId", "from"),
            NodeId(envelopePayload, "fromId", "from", "id", "nodeId"),
            NodeId(position, "fromId", "from", "nodeId"),
            NodeId(payload, "nodeId", "id"),
            NodeId(payload, "sender"));

        var label = FirstNonBlank(
            String(payload, "longName", "longname", "shortName", "shortname", "name"),
            String(packet, "longName", "longname", "shortName", "shortname", "name"),
            String(envelopePayload, "longName", "longname", "shortName", "shortname", "name"));

        var latitude = Double(position, "latitude", "lat");
        var longitude = Double(position, "longitude", "lon", "lng");

        latitude ??= ScaledCoordinate(Double(position, "latitudeI", "latitude_i", "latitudeIi"));
        longitude ??= ScaledCoordinate(Double(position, "longitudeI", "longitude_i", "longitudeIi"));
        var precisionBits = Double(position, "precisionBits", "precision_bits");
        var accuracyMeters = Double(position, "accuracyMeters", "accuracy_meters", "accuracy", "gpsAccuracy", "gps_accuracy");
        accuracyMeters ??= EstimateAccuracyMeters(precisionBits, Double(position, "PDOP", "pdop"), Double(position, "satsInView", "sats_in_view"));

        var observation = RequiredObservation(
            context,
            payload,
            "meshtastic",
            externalId,
            SelectObservedAt(payload, position),
            latitude,
            longitude,
            Double(position, "altitude", "altitudeMeters", "alt"),
            accuracyMeters,
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
                ["gatewaySender"] = String(payload, "sender"),
                ["topic"] = String(payload, "topic"),
                ["precisionBits"] = precisionBits,
                ["pdop"] = Double(position, "PDOP", "pdop"),
                ["satsInView"] = Double(position, "satsInView", "sats_in_view")
            });

        return ValueTask.FromResult<IReadOnlyList<ProviderObservation>>([observation]);
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? NodeId(JsonElement element, params string[] names)
    {
        var raw = String(element, names);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('!')) return trimmed;

        if (ulong.TryParse(trimmed, out var unsigned))
        {
            return $"!{unchecked((uint)unsigned):x8}";
        }

        if (long.TryParse(trimmed, out var signed))
        {
            return $"!{unchecked((uint)signed):x8}";
        }

        return trimmed;
    }

    private static DateTime? SelectObservedAt(JsonElement payload, JsonElement position)
    {
        var now = DateTime.UtcNow;
        var gatewayTime = DateTimeUtc(payload, "rxTime", "timestamp");
        var deviceTime = DateTimeUtc(position, "time", "timestamp", "observedAt");

        foreach (var candidate in new[] { gatewayTime, deviceTime })
        {
            if (candidate is null) continue;

            var utc = candidate.Value.Kind == DateTimeKind.Utc
                ? candidate.Value
                : DateTime.SpecifyKind(candidate.Value, DateTimeKind.Utc);

            if (utc <= now.AddMinutes(1))
            {
                return utc;
            }
        }

        return gatewayTime is not null || deviceTime is not null ? now : null;
    }

    private static double? ScaledCoordinate(double? integerCoordinate)
        => integerCoordinate is null ? null : integerCoordinate.Value / 10_000_000d;

    private static double? SpeedMetersPerSecondToKmh(double? metersPerSecond)
        => metersPerSecond is null ? null : Math.Round(metersPerSecond.Value * 3.6, 3);

    private static double? EstimateAccuracyMeters(double? precisionBits, double? pdop, double? satsInView)
    {
        if (precisionBits is not null)
        {
            var bits = Math.Clamp(precisionBits.Value, 0, 32);
            var quantizationMeters = Math.Pow(2, 32 - bits) * 0.011132d;
            return Math.Round(Math.Max(1.6d, quantizationMeters), 2);
        }

        if (pdop is not null)
        {
            var normalizedPdop = pdop > 50 ? pdop.Value / 100d : pdop.Value;
            var satellitePenalty = satsInView is > 0 and < 6 ? 2d : 1d;
            return Math.Round(Math.Max(1.6d, normalizedPdop * 5d * satellitePenalty), 2);
        }

        return null;
    }
}
