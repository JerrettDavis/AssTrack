using System.Text.Json;
using AssTrack.BridgeGateway.Services;
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

        bridge.MapPost("/{feedKey}/resync", (string feedKey, BridgeFeedMonitor monitor) =>
        {
            var version = monitor.RequestResync(feedKey);
            return Results.Ok(new { feedKey, resyncVersion = version });
        });

        return app;
    }
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
