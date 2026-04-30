using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Services;

public sealed class WebhookNotificationService : IWebhookNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public WebhookNotificationService(
        HttpClient httpClient,
        IOptions<WebhookOptions> options,
        ILogger<WebhookNotificationService> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

        await PostAsync(payload, "speed_alert", cancellationToken);
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

        await PostAsync(payload, "geofence_breach", cancellationToken);
    }

    private async Task PostAsync<T>(T payload, string eventType, CancellationToken cancellationToken)
    {
        var attemptedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int? httpStatusCode = null;
        bool success = false;
        string? errorMessage = null;

        var rawJson = JsonSerializer.Serialize(payload);
        var payloadSummary = rawJson.Length > 500 ? rawJson[..500] : rawJson;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await _httpClient.PostAsJsonAsync(_options.Url, payload, cts.Token);
            httpStatusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;

            if (!success)
            {
                _logger.LogWarning(
                    "Webhook delivery to {Url} returned non-success status {StatusCode}",
                    _options.Url, httpStatusCode);
                errorMessage = $"HTTP {httpStatusCode}";
            }
            else
            {
                _logger.LogDebug("Webhook delivered to {Url} with status {StatusCode}", _options.Url, httpStatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook delivery to {Url} failed; continuing.", _options.Url);
            errorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            await PersistLogAsync(new WebhookDeliveryLog
            {
                AttemptedAt = attemptedAt,
                EventType = eventType,
                TargetUrl = _options.Url!,
                Success = success,
                HttpStatusCode = httpStatusCode,
                DurationMs = (int)sw.ElapsedMilliseconds,
                ErrorMessage = errorMessage,
                RequestPayloadSummary = payloadSummary
            });
        }
    }

    private async Task PersistLogAsync(WebhookDeliveryLog log)
    {
        if (_scopeFactory is null)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            db.WebhookDeliveryLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist webhook delivery log; continuing.");
        }
    }
}
