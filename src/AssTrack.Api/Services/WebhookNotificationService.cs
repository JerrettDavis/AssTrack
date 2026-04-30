using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Services;

public sealed class WebhookNotificationService : IWebhookNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookNotificationService> _logger;

    public WebhookNotificationService(
        HttpClient httpClient,
        IOptions<WebhookOptions> options,
        ILogger<WebhookNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            return;

        var payload = new SpeedAlertWebhookPayload(
            EventType: "speed_alert",
            AlertId: alert.Id,
            DeviceId: alert.DeviceId,
            DeviceIdentifier: alert.Device?.Identifier,
            AssetId: alert.AssetId,
            AssetName: alert.Asset?.Name,
            ObservedSpeedKmh: alert.ObservedSpeedKmh,
            ThresholdKmh: alert.ThresholdKmh,
            TriggeredAt: alert.TriggeredAt,
            DeliveredAt: DateTime.UtcNow);

        await PostAsync(payload, cancellationToken);
    }

    public async Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            return;

        var payload = new GeofenceBreachWebhookPayload(
            EventType: "geofence_breach",
            BreachId: breach.Id,
            DeviceId: breach.DeviceId,
            DeviceIdentifier: breach.Device?.Identifier,
            AssetId: breach.AssetId,
            AssetName: breach.Asset?.Name,
            GeofenceId: breach.GeofenceId,
            GeofenceName: breach.Geofence?.Name ?? string.Empty,
            BreachEventType: breach.EventType.ToString(),
            DetectedAt: breach.DetectedAt,
            DeliveredAt: DateTime.UtcNow);

        await PostAsync(payload, cancellationToken);
    }

    private async Task PostAsync<T>(T payload, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await _httpClient.PostAsJsonAsync(_options.Url, payload, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Webhook delivery to {Url} returned non-success status {StatusCode}",
                    _options.Url, (int)response.StatusCode);
            }
            else
            {
                _logger.LogDebug("Webhook delivered to {Url} with status {StatusCode}", _options.Url, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook delivery to {Url} failed; continuing.", _options.Url);
        }
    }
}
