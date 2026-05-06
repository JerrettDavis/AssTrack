using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AssTrack.BridgeGateway.Services;

public sealed class HomeAssistantPollingService(
    IHttpClientFactory httpClientFactory,
    IOptions<BridgeGatewayOptions> options,
    DynamicBridgeFeedStore dynamicFeeds,
    IAssTrackIngestClient ingestClient,
    ILogger<HomeAssistantPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollFeedsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task PollFeedsAsync(CancellationToken cancellationToken)
    {
        if (options.Value.DryRun) return;

        foreach (var item in dynamicFeeds.Snapshot.Concat(options.Value.Feeds))
        {
            var feed = item.Value;
            if (!feed.Enabled || !IsProvider(feed, "home-assistant")) continue;
            if (!IsTrue(Get(feed, "pollingEnabled", "pollEnabled"))) continue;

            try
            {
                await PollFeedAsync(item.Key, feed, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Home Assistant polling failed for feed {FeedKey}.", item.Key);
            }
        }
    }

    private async Task PollFeedAsync(string feedKey, BridgeFeedOptions feed, CancellationToken cancellationToken)
    {
        var baseUrl = Get(feed, "baseUrl", "homeAssistantUrl", "url");
        var token = Get(feed, "accessToken", "token", "apiToken");
        var entities = Split(Get(feed, "entities", "entityIds", "deviceTrackers"));
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token) || entities.Count == 0)
        {
            logger.LogWarning("Home Assistant feed {FeedKey} is missing baseUrl, accessToken, or entities.", feedKey);
            return;
        }

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        foreach (var entityId in entities)
        {
            using var response = await client.GetAsync($"api/states/{Uri.EscapeDataString(entityId)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Home Assistant entity {EntityId} returned {StatusCode}.", entityId, response.StatusCode);
                continue;
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var observation = ParseState(feed, entityId, document.RootElement);
            if (observation is null) continue;

            var delivery = await ingestClient.SendAsync(feed.FeedId, observation, cancellationToken);
            if (!delivery.Success)
            {
                logger.LogWarning("Home Assistant delivery failed for {EntityId}: {StatusCode} {Body}", entityId, delivery.StatusCode, delivery.ResponseBody);
            }
        }
    }

    private static ProviderObservation? ParseState(BridgeFeedOptions feed, string entityId, JsonElement state)
    {
        var attributes = state.TryGetProperty("attributes", out var attr) ? attr : default;
        var lat = Double(attributes, "latitude", "lat");
        var lon = Double(attributes, "longitude", "lon", "lng");
        if (lat is null || lon is null) return null;

        var observedAt = DateTimeValue(state, "last_updated", "last_changed") ?? DateTime.UtcNow;
        var label = String(attributes, "friendly_name") ?? entityId;
        var tags = Merge(feed.DefaultTags, "home-assistant");

        return new ProviderObservation(
            entityId,
            observedAt,
            lat.Value,
            lon.Value,
            Double(attributes, "altitude"),
            Double(attributes, "gps_accuracy", "accuracy", "accuracyMeters"),
            Double(attributes, "speed", "speedKmh"),
            Double(attributes, "course", "heading", "headingDegrees"),
            label,
            feed.AssetId,
            tags,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["adapter"] = "home-assistant-rest",
                ["entityId"] = entityId,
                ["state"] = String(state, "state")
            }));
    }

    private static bool IsProvider(BridgeFeedOptions feed, string provider)
        => string.Equals(feed.Provider, provider, StringComparison.OrdinalIgnoreCase);

    private static string? Get(BridgeFeedOptions feed, params string[] keys)
        => keys.Select(key => feed.Settings.TryGetValue(key, out var value) ? value.Trim() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsTrue(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static IReadOnlyList<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? String(JsonElement element, params string[] names)
        => names.Select(name => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static double? Double(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number)) return number;
        }

        return null;
    }

    private static DateTime? DateTimeValue(JsonElement element, params string[] names)
        => names.Select(name => String(element, name))
            .Where(value => DateTime.TryParse(value, out _))
            .Select(value => DateTime.Parse(value!).ToUniversalTime())
            .Cast<DateTime?>()
            .FirstOrDefault();

    private static string? Merge(string? left, string right)
        => string.Join(", ", new[] { left, right }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase));
}
