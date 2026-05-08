using System.Text.Json;
using AssTrack.BridgeGateway.Services;
using AssTrack.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace AssTrack.BridgeGateway.Endpoints;

public static class BridgeGatewayEndpoints
{
    public static IEndpointRouteBuilder MapBridgeGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        var bridge = app.MapGroup("/bridge");

        bridge.MapGet("/providers", (ProviderAdapterRegistry registry) => Results.Ok(registry.Providers));

        bridge.MapGet("/status", (BridgeFeedMonitor monitor, IOptions<BridgeGatewayOptions> options) =>
            Results.Ok(new BridgeGatewayStatus(options.Value.DryRun, monitor.Statuses)));

        bridge.MapGet("/logs", (BridgeFeedMonitor monitor, string? feedKey, int? limit) =>
            Results.Ok(monitor.Logs(feedKey, limit ?? 100)));

        bridge.MapGet("/messages", (
            BridgeFeedMonitor monitor,
            string? feedKey,
            string? search,
            string? trackerId,
            string? topic,
            string? messageType,
            bool? payloadOnly,
            int? limit) =>
            Results.Ok(monitor.Messages(feedKey, search, trackerId, topic, messageType, payloadOnly ?? false, limit ?? 100)));

        bridge.MapGet("/feeds", (IOptions<BridgeGatewayOptions> options, DynamicBridgeFeedStore dynamicFeeds) =>
        {
            var feeds = dynamicFeeds.Snapshot.Concat(options.Value.Feeds)
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(item => new BridgeFeedSummary(
                item.Key,
                item.Value.Enabled,
                item.Value.FeedId,
                item.Value.Provider,
                item.Value.AssetId,
                item.Value.DefaultTags,
                HasSharedSecret: !string.IsNullOrWhiteSpace(item.Value.SharedSecret)));

            return Results.Ok(feeds);
        });

        bridge.MapPost("/{feedKey}", async (
            string feedKey,
            HttpRequest request,
            JsonElement payload,
            BridgeRequestHandler handler,
            CancellationToken cancellationToken) =>
        {
            var secret = request.Headers["X-Bridge-Secret"].ToString();
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = request.Query["secret"].ToString();
            }

            var result = await handler.HandleAsync(feedKey, secret, payload, cancellationToken);
            if (result.Deliveries.Count == 1 && result.ObservationsReceived == 0 && !result.Deliveries[0].Success)
            {
                return Results.Json(result, statusCode: result.Deliveries[0].StatusCode);
            }

            if (result.Deliveries.Any(item => !item.Success && !item.Retryable))
            {
                return Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
            }

            if (result.Deliveries.Any(item => !item.Success && item.Retryable))
            {
                return Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(result);
        });

        bridge.MapGet("/{feedKey}/messages/outbound", async (
            string feedKey,
            HttpRequest request,
            int? take,
            IOptions<BridgeGatewayOptions> options,
            DynamicBridgeFeedStore dynamicFeeds,
            IAssTrackIngestClient ingestClient,
            BridgeFeedMonitor monitor,
            CancellationToken cancellationToken) =>
        {
            var resolution = ResolveFeed(feedKey, request, options.Value, dynamicFeeds, monitor);
            if (resolution.Error is not null) return resolution.Error;

            if (options.Value.DryRun)
            {
                return Results.Ok(Array.Empty<OutboundMessageDto>());
            }

            var result = await ingestClient.GetOutboundMessagesAsync(resolution.Feed!.FeedId, take ?? 50, cancellationToken);
            if (!result.Success)
            {
                monitor.Log(feedKey, result.Retryable ? "warn" : "error", $"Outbound message queue fetch failed with {result.StatusCode}.");
                return Results.Json(new BridgeMessageGatewayResponse(
                    feedKey,
                    resolution.Provider,
                    resolution.Feed.FeedId,
                    false,
                    false,
                    result.StatusCode,
                    result.ResponseBody),
                    statusCode: result.Retryable ? StatusCodes.Status503ServiceUnavailable : result.StatusCode);
            }

            monitor.Update(feedKey, status =>
            {
                status.LastMessageAt = DateTime.UtcNow;
                status.LastError = null;
            });
            return Results.Ok(result.Messages);
        });

        bridge.MapPost("/{feedKey}/messages/inbound", async (
            string feedKey,
            HttpRequest request,
            BridgeInboundMessageRequest payload,
            IOptions<BridgeGatewayOptions> options,
            DynamicBridgeFeedStore dynamicFeeds,
            IAssTrackIngestClient ingestClient,
            BridgeFeedMonitor monitor,
            CancellationToken cancellationToken) =>
        {
            var resolution = ResolveFeed(feedKey, request, options.Value, dynamicFeeds, monitor);
            if (resolution.Error is not null) return resolution.Error;

            if (string.IsNullOrWhiteSpace(payload.ExternalPeerId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["externalPeerId"] = ["External peer id is required."] });
            }

            if (string.IsNullOrWhiteSpace(payload.Body))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Message body is required."] });
            }

            var feed = resolution.Feed!;
            var provider = resolution.Provider;
            var message = new InboundMessageRequest(
                Normalize(payload.Channel) ?? GetSetting(feed, "messageChannel", "channel") ?? "direct",
                provider,
                feed.FeedId,
                payload.DeviceId,
                payload.AssetId ?? feed.AssetId,
                payload.ExternalPeerId.Trim(),
                Normalize(payload.DisplayName) ?? Normalize(payload.Sender) ?? payload.ExternalPeerId.Trim(),
                Normalize(payload.Sender) ?? payload.ExternalPeerId.Trim(),
                payload.Body.Trim(),
                Normalize(payload.ProviderMessageId),
                payload.ReceivedAt,
                Normalize(payload.Metadata));

            if (options.Value.DryRun)
            {
                monitor.Log(feedKey, "info", $"Dry run accepted inbound {provider} message from {message.ExternalPeerId}; delivery skipped.");
                return Results.Accepted(null, new BridgeMessageGatewayResponse(feedKey, provider, feed.FeedId, true, true, StatusCodes.Status202Accepted, null));
            }

            var result = await ingestClient.SendInboundMessageAsync(message, cancellationToken);
            monitor.Update(feedKey, status =>
            {
                status.LastMessageAt = DateTime.UtcNow;
                status.MessagesReceived++;
                if (result.Success)
                {
                    status.LastDeliveryAt = DateTime.UtcNow;
                    status.LastError = null;
                }
                else
                {
                    status.DeliveryFailures++;
                    status.LastError = $"Inbound message delivery failed with {result.StatusCode}.";
                }
            });

            return Results.Json(new BridgeMessageGatewayResponse(
                feedKey,
                provider,
                feed.FeedId,
                false,
                result.Success,
                result.StatusCode,
                result.ResponseBody),
                statusCode: result.Success ? StatusCodes.Status202Accepted : result.Retryable ? StatusCodes.Status503ServiceUnavailable : result.StatusCode);
        });

        bridge.MapPost("/{feedKey}/messages/{messageId:guid}/status", async (
            string feedKey,
            Guid messageId,
            HttpRequest request,
            UpdateMessageStatusRequest payload,
            IOptions<BridgeGatewayOptions> options,
            DynamicBridgeFeedStore dynamicFeeds,
            IAssTrackIngestClient ingestClient,
            BridgeFeedMonitor monitor,
            CancellationToken cancellationToken) =>
        {
            var resolution = ResolveFeed(feedKey, request, options.Value, dynamicFeeds, monitor);
            if (resolution.Error is not null) return resolution.Error;

            if (options.Value.DryRun)
            {
                return Results.Accepted(null, new BridgeMessageGatewayResponse(feedKey, resolution.Provider, resolution.Feed!.FeedId, true, true, StatusCodes.Status202Accepted, null));
            }

            var result = await ingestClient.UpdateMessageStatusAsync(messageId, payload, cancellationToken);
            monitor.Update(feedKey, status =>
            {
                if (result.Success)
                {
                    status.LastDeliveryAt = DateTime.UtcNow;
                    status.LastError = null;
                }
                else
                {
                    status.DeliveryFailures++;
                    status.LastError = $"Message status update failed with {result.StatusCode}.";
                }
            });

            return Results.Json(new BridgeMessageGatewayResponse(
                feedKey,
                resolution.Provider,
                resolution.Feed!.FeedId,
                false,
                result.Success,
                result.StatusCode,
                result.ResponseBody),
                statusCode: result.Success ? StatusCodes.Status202Accepted : result.Retryable ? StatusCodes.Status503ServiceUnavailable : result.StatusCode);
        });

        bridge.MapPost("/{feedKey}/resync", (string feedKey, BridgeFeedMonitor monitor) =>
        {
            var version = monitor.RequestResync(feedKey);
            return Results.Ok(new { feedKey, resyncVersion = version });
        });

        return app;
    }

    private static BridgeFeedResolution ResolveFeed(
        string feedKey,
        HttpRequest request,
        BridgeGatewayOptions config,
        DynamicBridgeFeedStore dynamicFeeds,
        BridgeFeedMonitor monitor)
    {
        if (!dynamicFeeds.TryGet(feedKey, out var feed) && !config.Feeds.TryGetValue(feedKey, out feed))
        {
            monitor.Log(feedKey, "warn", "Message bridge request rejected because feed is not configured.");
            return BridgeFeedResolution.Failed(Results.Json(new { feedKey, error = "Feed is not configured." }, statusCode: StatusCodes.Status404NotFound));
        }

        var provider = string.IsNullOrWhiteSpace(feed.Provider) ? "generic-webhook" : feed.Provider.Trim();
        monitor.Update(feedKey, status =>
        {
            status.FeedId = feed.FeedId;
            status.Provider = provider;
            status.State = feed.Enabled ? "message-request" : "disabled";
        });

        if (!feed.Enabled)
        {
            return BridgeFeedResolution.Failed(Results.Json(new { feedKey, error = "Feed is disabled." }, statusCode: StatusCodes.Status400BadRequest));
        }

        if (feed.FeedId == Guid.Empty)
        {
            return BridgeFeedResolution.Failed(Results.Json(new { feedKey, error = "FeedId is required." }, statusCode: StatusCodes.Status400BadRequest));
        }

        var secret = request.Headers["X-Bridge-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = request.Query["secret"].ToString();
        }

        if (!string.IsNullOrWhiteSpace(feed.SharedSecret) && !string.Equals(feed.SharedSecret, secret, StringComparison.Ordinal))
        {
            return BridgeFeedResolution.Failed(Results.Json(new { feedKey, error = "Invalid bridge secret." }, statusCode: StatusCodes.Status401Unauthorized));
        }

        return new BridgeFeedResolution(feed, provider, null);
    }

    private static string? GetSetting(BridgeFeedOptions feed, params string[] keys)
        => keys.Select(key => feed.Settings.TryGetValue(key, out var value) ? Normalize(value) : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record BridgeInboundMessageRequest(
    string ExternalPeerId,
    string Body,
    string? Channel,
    string? DisplayName,
    string? Sender,
    string? ProviderMessageId,
    DateTime? ReceivedAt,
    Guid? DeviceId,
    Guid? AssetId,
    string? Metadata);

public sealed record BridgeMessageGatewayResponse(
    string FeedKey,
    string Provider,
    Guid FeedId,
    bool DryRun,
    bool Success,
    int StatusCode,
    string? ResponseBody);

public sealed record BridgeFeedResolution(BridgeFeedOptions? Feed, string Provider, IResult? Error)
{
    public static BridgeFeedResolution Failed(IResult error) => new(null, "unknown", error);
}

public sealed record BridgeFeedSummary(
    string Key,
    bool Enabled,
    Guid FeedId,
    string Provider,
    Guid? AssetId,
    string? DefaultTags,
    bool HasSharedSecret);

public sealed record BridgeGatewayStatus(bool DryRun, IReadOnlyList<BridgeFeedRuntimeStatus> Feeds);
