using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace AssTrack.BridgeGateway.Services;

public sealed class AssTrackIngestClient(HttpClient httpClient, IOptions<BridgeGatewayOptions> options) : IAssTrackIngestClient
{
    public async Task<BridgeDeliveryResult> SendAsync(Guid feedId, ProviderObservation observation, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (config.AssTrackBaseUrl is null)
        {
            throw new InvalidOperationException("BridgeGateway:AssTrackBaseUrl is required when DryRun is false.");
        }

        if (string.IsNullOrWhiteSpace(config.IngestApiKey))
        {
            throw new InvalidOperationException("BridgeGateway:IngestApiKey is required when DryRun is false.");
        }

        var url = new Uri(config.AssTrackBaseUrl, $"/api/integrations/{feedId}/observations");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new IntegrationFeedObservationRequest(
                observation.ExternalTrackerId,
                observation.ObservedAt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(observation.ObservedAt, DateTimeKind.Utc)
                    : observation.ObservedAt.ToUniversalTime(),
                observation.Latitude,
                observation.Longitude,
                observation.Altitude,
                observation.AccuracyMeters,
                observation.SpeedKmh,
                observation.HeadingDegrees,
                observation.Label,
                observation.AssetId,
                observation.Tags,
                observation.Metadata))
        };

        request.Headers.TryAddWithoutValidation("X-Api-Key", config.IngestApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new BridgeDeliveryResult(response.IsSuccessStatusCode, (int)response.StatusCode, body, IsRetryable(response.StatusCode));
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
