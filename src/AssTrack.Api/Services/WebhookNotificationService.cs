using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace AssTrack.Api.Services;

public sealed class WebhookNotificationService : IWebhookNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ChannelWriter<WebhookRetryJob>? _retryWriter;
    private readonly WebhookSubscriptionRepository? _subscriptionRepository;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Status codes that warrant a background retry attempt.</summary>
    public static readonly IReadOnlySet<int> TransientStatusCodes =
        new HashSet<int> { 429, 500, 502, 503, 504 };

    public WebhookNotificationService(
        HttpClient httpClient,
        IOptions<WebhookOptions> options,
        ILogger<WebhookNotificationService> logger,
        IServiceScopeFactory? scopeFactory = null,
        ChannelWriter<WebhookRetryJob>? retryWriter = null,
        WebhookSubscriptionRepository? subscriptionRepository = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _retryWriter = retryWriter;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
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

        var rawJson = JsonSerializer.Serialize(payload, _jsonOptions);
        await SendWebhookAttemptAsync(rawJson, "speed_alert", 1, correlationId, cancellationToken);
    }

    public async Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
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

        var rawJson = JsonSerializer.Serialize(payload, _jsonOptions);
        await SendWebhookAttemptAsync(rawJson, "geofence_breach", 1, correlationId, cancellationToken);
    }

    public async Task NotifyIntegrationEventAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var correlationId = string.IsNullOrWhiteSpace(integrationEvent.CorrelationId)
            ? Guid.NewGuid().ToString()
            : integrationEvent.CorrelationId;
        var payload = new IntegrationEventWebhookPayload(
            EventType: IntegrationEventTypes.EnterpriseSignal,
            EventId: integrationEvent.Id,
            Source: integrationEvent.Source,
            SignalType: integrationEvent.EventType,
            Severity: integrationEvent.Severity,
            SubjectType: integrationEvent.SubjectType,
            SubjectId: integrationEvent.SubjectId,
            SubjectName: integrationEvent.SubjectName,
            Message: integrationEvent.Message,
            PayloadJson: integrationEvent.PayloadJson,
            OccurredAt: integrationEvent.OccurredAt,
            DeliveredAt: DateTime.UtcNow,
            CorrelationId: correlationId);

        var rawJson = JsonSerializer.Serialize(payload, _jsonOptions);
        await SendWebhookAttemptAsync(rawJson, IntegrationEventTypes.EnterpriseSignal, 1, correlationId!, cancellationToken);
    }

    public async Task ExecuteRetryAsync(WebhookRetryJob job, CancellationToken cancellationToken = default)
    {
        var targetUrl = job.TargetUrl ?? _options.Url;
        if (string.IsNullOrWhiteSpace(targetUrl))
            return;

        // Determine event type from the stored payload summary fallback; use empty string if unknown
        var signingSecret = job.TargetUrl is null ? _options.SigningSecret : job.SigningSecret;
        await SendWebhookAttemptAsync(
            job.Payload,
            eventType: job.EventType,
            job.AttemptNumber,
            job.CorrelationId,
            cancellationToken,
            isRetry: true,
            targetOverride: new WebhookDeliveryTarget(targetUrl, signingSecret));
    }

    /// <summary>
    /// Core send method shared by first-attempt and retry paths.
    /// On transient failure, enqueues the next retry if retries remain.
    /// </summary>
    internal async Task SendWebhookAttemptAsync(
        string rawJson,
        string eventType,
        int attemptNumber,
        string correlationId,
        CancellationToken cancellationToken,
        bool isRetry = false,
        WebhookDeliveryTarget? targetOverride = null)
    {
        var targets = targetOverride is not null
            ? [targetOverride]
            : await GetTargetsAsync(eventType, cancellationToken);

        foreach (var target in targets)
        {
            await SendTargetAttemptAsync(rawJson, eventType, attemptNumber, correlationId, target, cancellationToken, isRetry);
        }
    }

    private async Task SendTargetAttemptAsync(
        string rawJson,
        string eventType,
        int attemptNumber,
        string correlationId,
        WebhookDeliveryTarget target,
        CancellationToken cancellationToken,
        bool isRetry)
    {
        var attemptedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int? httpStatusCode = null;
        bool success = false;
        string? errorMessage = null;
        bool isTransient = false;

        var payloadSummary = rawJson.Length > 500 ? rawJson[..500] : rawJson;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, target.Url) { Content = content };

            if (!string.IsNullOrEmpty(target.SigningSecret))
            {
                var keyBytes = Encoding.UTF8.GetBytes(target.SigningSecret);
                var bodyBytes = Encoding.UTF8.GetBytes(rawJson);
                using var hmac = new HMACSHA256(keyBytes);
                var hash = hmac.ComputeHash(bodyBytes);
                var sig = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
                request.Headers.Add("X-AssTrack-Signature-256", sig);
            }

            var response = await _httpClient.SendAsync(request, cts.Token);
            httpStatusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;

            if (success)
            {
                if (isRetry)
                    _logger.LogInformation(
                        "Webhook retry succeeded on attempt {Attempt} for correlation {CorrelationId} → {StatusCode}",
                        attemptNumber, correlationId, httpStatusCode);
                else
                    _logger.LogDebug("Webhook delivered to {Url} with status {StatusCode}", target.Url, httpStatusCode);
            }
            else
            {
                isTransient = httpStatusCode.HasValue && TransientStatusCodes.Contains(httpStatusCode.Value);
                errorMessage = $"HTTP {httpStatusCode}";
                _logger.LogWarning(
                    "Webhook delivery to {Url} returned non-success status {StatusCode} (attempt {Attempt}, correlationId {CorrelationId})",
                    target.Url, httpStatusCode, attemptNumber, correlationId);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            isTransient = true;
            errorMessage = ex.Message;
            _logger.LogError(ex, "Webhook delivery to {Url} failed with transient error; continuing.", target.Url);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Webhook delivery to {Url} failed; continuing.", target.Url);
        }
        finally
        {
            sw.Stop();
            await PersistLogAsync(new WebhookDeliveryLog
            {
                AttemptedAt = attemptedAt,
                EventType = eventType,
                TargetUrl = target.Url,
                Success = success,
                HttpStatusCode = httpStatusCode,
                DurationMs = (int)sw.ElapsedMilliseconds,
                ErrorMessage = errorMessage,
                RequestPayloadJson = rawJson,
                RequestPayloadSummary = payloadSummary,
                AttemptNumber = attemptNumber,
                CorrelationId = correlationId
            });
        }

        // Enqueue retry if this was a transient failure and retries are configured
        if (!success && isTransient && attemptNumber <= _options.MaxRetries && _retryWriter is not null)
        {
            var nextAttempt = attemptNumber + 1;
            var job = new WebhookRetryJob(
                WebhookId: Guid.NewGuid(),
                Payload: rawJson,
                EventType: eventType,
                AttemptNumber: nextAttempt,
                CorrelationId: correlationId,
                ScheduledAt: DateTime.UtcNow)
            {
                TargetUrl = target.Url,
                SigningSecret = target.SigningSecret
            };

            if (_retryWriter.TryWrite(job))
                _logger.LogWarning(
                    "Webhook retry enqueued: attempt {NextAttempt}/{MaxRetries}, correlationId {CorrelationId}",
                    nextAttempt, _options.MaxRetries, correlationId);
            else
                _logger.LogWarning(
                    "Webhook retry channel full – dropped retry attempt {NextAttempt}, correlationId {CorrelationId}",
                    nextAttempt, correlationId);
        }
        else if (!success && isTransient && attemptNumber > _options.MaxRetries && isRetry)
        {
            _logger.LogError(
                "Webhook delivery failed after {MaxRetries} retries; giving up. CorrelationId {CorrelationId}",
                _options.MaxRetries, correlationId);
        }
    }

    private async Task<IReadOnlyList<WebhookDeliveryTarget>> GetTargetsAsync(string eventType, CancellationToken cancellationToken)
    {
        var targets = new List<WebhookDeliveryTarget>();
        if (!string.IsNullOrWhiteSpace(_options.Url))
        {
            targets.Add(new WebhookDeliveryTarget(_options.Url, _options.SigningSecret));
        }

        if (_subscriptionRepository is not null)
        {
            var subscriptions = await _subscriptionRepository.GetEnabledForEventAsync(eventType, cancellationToken);
            targets.AddRange(subscriptions.Select(x => new WebhookDeliveryTarget(x.TargetUrl, x.SigningSecret)));
        }

        return targets;
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

            var broadcaster = scope.ServiceProvider.GetService<ILiveEventBroadcaster>();
            broadcaster?.PublishDataChanged("webhook_delivery", "created", metadata: new { log.Id, log.EventType, log.Success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist webhook delivery log; continuing.");
        }
    }

    internal sealed record WebhookDeliveryTarget(string Url, string? SigningSecret);
}
