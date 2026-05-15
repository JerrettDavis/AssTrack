using System.Collections.Concurrent;
using System.Text.Json;
using AssTrack.Domain.Contracts;

namespace AssTrack.BridgeGateway.Services;

public sealed class DynamicBridgeFeedStore
{
    private readonly ConcurrentDictionary<string, BridgeFeedOptions> _feeds = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, BridgeFeedOptions> Snapshot => _feeds;

    public bool TryGet(string feedKey, out BridgeFeedOptions feed) => _feeds.TryGetValue(feedKey, out feed!);

    public void Replace(IEnumerable<BridgeIntegrationFeedConfigDto> feeds)
    {
        var next = feeds
            .Select(Map)
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => item.Feed, StringComparer.OrdinalIgnoreCase);

        _feeds.Clear();
        foreach (var item in next)
        {
            _feeds[item.Key] = item.Value;
        }
    }

    private static (string Key, BridgeFeedOptions Feed) Map(BridgeIntegrationFeedConfigDto feed)
    {
        var settings = ParseSettings(feed.ConfigurationJson);
        var key = Get(settings, "bridgeKey", "feedKey", "key") ?? Slug(feed.Name);
        var bridgeEnabled = !IsFalse(Get(settings, "bridgeEnabled", "enabled"));

        return (key, new BridgeFeedOptions
        {
            Enabled = feed.IsEnabled && bridgeEnabled,
            FeedId = feed.FeedId,
            Provider = feed.Provider,
            SharedSecret = Get(settings, "sharedSecret", "bridgeSecret", "secret"),
            DefaultTags = feed.DefaultTags,
            ConfigurationJson = feed.ConfigurationJson,
            Settings = settings
        });
    }

    private static Dictionary<string, string> ParseSettings(string? json)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return settings;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return settings;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) return settings;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                settings[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.GetRawText(),
                    JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(ToScalarString).Where(x => !string.IsNullOrWhiteSpace(x))),
                    _ => property.Value.GetRawText()
                };
            }
        }

        return settings;
    }

    private static string? Get(IReadOnlyDictionary<string, string> settings, params string[] keys)
        => keys.Select(key => settings.TryGetValue(key, out var value) ? value.Trim() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsFalse(string? value)
        => string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0";

    private static string ToScalarString(JsonElement item)
        => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText();

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }
}
