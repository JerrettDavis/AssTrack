using System.Text.Json;
using AssTrack.BridgeGateway.Adapters;
using Microsoft.Extensions.Options;

namespace AssTrack.BridgeGateway.Services;

public sealed class BridgeRequestHandler(
    IOptions<BridgeGatewayOptions> options,
    DynamicBridgeFeedStore dynamicFeeds,
    ProviderAdapterRegistry registry,
    IAssTrackIngestClient ingestClient,
    BridgeFeedMonitor monitor)
{
    public async Task<BridgeIngestResponse> HandleAsync(string feedKey, string? providedSecret, JsonElement payload, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!dynamicFeeds.TryGet(feedKey, out var feed) && !config.Feeds.TryGetValue(feedKey, out feed))
        {
            monitor.Log(feedKey, "warn", "HTTP bridge request rejected because feed is not configured.");
            return BridgeIngestResponse.Failed(feedKey, "Feed is not configured.", StatusCodes.Status404NotFound);
        }

        monitor.Update(feedKey, status =>
        {
            status.FeedId = feed.FeedId;
            status.Provider = feed.Provider;
            status.State = feed.Enabled ? "http-request" : "disabled";
        });

        if (!feed.Enabled)
        {
            return BridgeIngestResponse.Failed(feedKey, "Feed is disabled.", StatusCodes.Status400BadRequest);
        }

        if (feed.FeedId == Guid.Empty)
        {
            return BridgeIngestResponse.Failed(feedKey, "FeedId is required.", StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(feed.SharedSecret) && !string.Equals(feed.SharedSecret, providedSecret, StringComparison.Ordinal))
        {
            return BridgeIngestResponse.Failed(feedKey, "Invalid bridge secret.", StatusCodes.Status401Unauthorized);
        }

        var provider = string.IsNullOrWhiteSpace(feed.Provider) ? "generic-webhook" : feed.Provider.Trim();
        var adapter = registry.Get(provider);
        if (adapter is null)
        {
            return BridgeIngestResponse.Failed(feedKey, $"Provider '{provider}' is not supported by this gateway.", StatusCodes.Status400BadRequest);
        }

        IReadOnlyList<ProviderObservation> observations;
        try
        {
            observations = await adapter.ParseAsync(new BridgeFeedContext(feedKey, feed, provider), payload, cancellationToken);
            monitor.Update(feedKey, status =>
            {
                status.LastMessageAt = DateTime.UtcNow;
                status.MessagesReceived++;
                status.ObservationsParsed += observations.Count;
                status.LastError = null;
            });
        }
        catch (BridgePayloadException ex)
        {
            monitor.Log(feedKey, "warn", ex.Message);
            return BridgeIngestResponse.Failed(feedKey, ex.Message, StatusCodes.Status400BadRequest);
        }

        if (observations.Count == 0)
        {
            return new BridgeIngestResponse(feedKey, provider, feed.FeedId, 0, 0, config.DryRun, [], []);
        }

        if (config.DryRun)
        {
            monitor.Log(feedKey, "info", $"Dry run parsed {observations.Count} observation(s); delivery skipped.");
            return new BridgeIngestResponse(feedKey, provider, feed.FeedId, observations.Count, 0, true, observations, []);
        }

        var deliveries = new List<BridgeObservationDelivery>();
        foreach (var observation in observations)
        {
            var delivery = await ingestClient.SendAsync(feed.FeedId, observation, cancellationToken);
            deliveries.Add(new BridgeObservationDelivery(observation.ExternalTrackerId, delivery.Success, delivery.StatusCode, delivery.Retryable, delivery.ResponseBody));
            monitor.Update(feedKey, status =>
            {
                status.LastTrackerId = observation.ExternalTrackerId;
                if (delivery.Success)
                {
                    status.ObservationsDelivered++;
                    status.LastDeliveryAt = DateTime.UtcNow;
                    status.LastError = null;
                }
                else
                {
                    status.DeliveryFailures++;
                    status.LastError = $"Delivery failed with {delivery.StatusCode}.";
                }
            });
        }

        return new BridgeIngestResponse(feedKey, provider, feed.FeedId, observations.Count, deliveries.Count(x => x.Success), false, [], deliveries);
    }
}

public sealed record BridgeIngestResponse(
    string FeedKey,
    string Provider,
    Guid FeedId,
    int ObservationsReceived,
    int ObservationsDelivered,
    bool DryRun,
    IReadOnlyList<ProviderObservation> ParsedObservations,
    IReadOnlyList<BridgeObservationDelivery> Deliveries)
{
    public static BridgeIngestResponse Failed(string feedKey, string message, int statusCode)
        => new(feedKey, "unknown", Guid.Empty, 0, 0, false, [], [new BridgeObservationDelivery(null, false, statusCode, false, message)]);
}

public sealed record BridgeObservationDelivery(string? ExternalTrackerId, bool Success, int StatusCode, bool Retryable, string? ResponseBody);
