using System.Globalization;
using System.Text.Json;

namespace AssTrack.BridgeGateway.Adapters;

public abstract class ProviderPayloadAdapterBase : IProviderPayloadAdapter
{
    public abstract string Provider { get; }
    public virtual IReadOnlySet<string> Aliases => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public abstract ValueTask<IReadOnlyList<ProviderObservation>> ParseAsync(BridgeFeedContext context, JsonElement payload, CancellationToken cancellationToken);

    protected static JsonElement? Find(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var name in names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    protected static string? String(JsonElement element, params string[] names)
    {
        var value = Find(element, names);
        if (value is null) return null;
        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    protected static double? Double(JsonElement element, params string[] names)
    {
        var value = Find(element, names);
        if (value is null) return null;
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number)) return number;
        if (value.Value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    protected static DateTime? DateTimeUtc(JsonElement element, params string[] names)
    {
        var value = Find(element, names);
        if (value is null) return null;

        if (value.Value.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(value.Value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime))
        {
            return dateTime;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt64(out var epoch))
        {
            return epoch > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        return null;
    }

    protected static Guid? GuidValue(JsonElement element, params string[] names)
    {
        var value = String(element, names);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    protected static string RawMetadata(string provider, JsonElement payload, Dictionary<string, object?>? extra = null)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["gatewayProvider"] = provider,
            ["raw"] = JsonSerializer.Deserialize<object?>(payload.GetRawText())
        };

        if (extra is not null)
        {
            foreach (var item in extra)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return JsonSerializer.Serialize(metadata);
    }

    protected static ProviderObservation WithFeedDefaults(BridgeFeedContext context, ProviderObservation observation)
    {
        var label = observation.Label;
        if (!string.IsNullOrWhiteSpace(context.Feed.LabelPrefix))
        {
            label = string.IsNullOrWhiteSpace(label)
                ? $"{context.Feed.LabelPrefix} {observation.ExternalTrackerId}"
                : $"{context.Feed.LabelPrefix} {label}";
        }

        return observation with
        {
            AssetId = observation.AssetId ?? context.Feed.AssetId,
            Tags = MergeTags(context.Feed.DefaultTags, observation.Tags),
            Label = label
        };
    }

    protected static string? MergeTags(string? left, string? right)
    {
        var values = new[] { left, right }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    protected static ProviderObservation RequiredObservation(
        BridgeFeedContext context,
        JsonElement payload,
        string provider,
        string? externalTrackerId,
        DateTime? observedAt,
        double? latitude,
        double? longitude,
        double? altitude,
        double? accuracyMeters,
        double? speedKmh,
        double? headingDegrees,
        string? label,
        Guid? assetId,
        string? tags,
        Dictionary<string, object?>? metadataExtra = null)
    {
        if (string.IsNullOrWhiteSpace(externalTrackerId))
        {
            throw new BridgePayloadException("Payload did not include an external tracker id.");
        }

        if (latitude is null || longitude is null)
        {
            throw new BridgePayloadException("Payload did not include latitude and longitude.");
        }

        if (latitude is < -90 or > 90)
        {
            throw new BridgePayloadException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new BridgePayloadException("Longitude must be between -180 and 180.");
        }

        return WithFeedDefaults(context, new ProviderObservation(
            externalTrackerId.Trim(),
            observedAt ?? DateTime.UtcNow,
            latitude.Value,
            longitude.Value,
            altitude,
            accuracyMeters,
            speedKmh,
            headingDegrees,
            label,
            assetId,
            tags,
            RawMetadata(provider, payload, metadataExtra)));
    }
}
