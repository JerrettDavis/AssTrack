using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace AssTrack.BridgeGateway.Services;

public sealed class BridgeConfigRefreshService(
    HttpClient httpClient,
    IOptions<BridgeGatewayOptions> options,
    DynamicBridgeFeedStore store,
    ILogger<BridgeConfigRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            var delay = TimeSpan.FromSeconds(Math.Max(10, options.Value.BridgeConfigRefreshSeconds));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (config.AssTrackBaseUrl is null || string.IsNullOrWhiteSpace(config.OperatorApiKey))
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(config.AssTrackBaseUrl, "/api/integrations/bridge-config"));
            request.Headers.TryAddWithoutValidation("X-Api-Key", config.OperatorApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Bridge config refresh failed with {StatusCode}.", response.StatusCode);
                return;
            }

            var feeds = await response.Content.ReadFromJsonAsync<List<BridgeIntegrationFeedConfigDto>>(cancellationToken);
            store.Replace(feeds ?? []);
            logger.LogInformation("Loaded {Count} dynamic bridge feed configs.", feeds?.Count ?? 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bridge config refresh failed.");
        }
    }
}
