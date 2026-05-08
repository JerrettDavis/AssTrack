using System.Text.Json;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Services;

public interface IAlertRoutingService
{
    Task RouteSpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default);
    Task RouteGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default);
}

public sealed class AlertRoutingService(
    AlertRoutingRuleRepository routeRepository,
    MessageRepository messageRepository,
    ILiveEventBroadcaster broadcaster,
    ILogger<AlertRoutingService> logger) : IAlertRoutingService
{
    public Task RouteSpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default)
        => RouteAsync(AlertRouteEventTypes.SpeedAlert, alert.DeviceId, alert.AssetId, BuildSpeedAlertBody(alert), new
        {
            eventType = AlertRouteEventTypes.SpeedAlert,
            alertId = alert.Id,
            observationId = alert.ObservationId,
            deviceId = alert.DeviceId,
            assetId = alert.AssetId,
            observedSpeedKmh = alert.ObservedSpeedKmh,
            thresholdKmh = alert.ThresholdKmh,
            triggeredAt = ApiDateTime.Utc(alert.TriggeredAt)
        }, cancellationToken);

    public Task RouteGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
        => RouteAsync(AlertRouteEventTypes.GeofenceBreach, breach.DeviceId, breach.AssetId, BuildGeofenceBreachBody(breach), new
        {
            eventType = AlertRouteEventTypes.GeofenceBreach,
            breachId = breach.Id,
            observationId = breach.ObservationId,
            geofenceId = breach.GeofenceId,
            geofenceName = breach.Geofence?.Name,
            breachEventType = breach.EventType.ToString(),
            deviceId = breach.DeviceId,
            assetId = breach.AssetId,
            detectedAt = ApiDateTime.Utc(breach.DetectedAt)
        }, cancellationToken);

    private async Task RouteAsync(
        string eventType,
        Guid deviceId,
        Guid? assetId,
        string defaultBody,
        object metadata,
        CancellationToken cancellationToken)
    {
        var routes = await routeRepository.GetEnabledForEventAsync(eventType, cancellationToken);
        foreach (var route in routes)
        {
            try
            {
                var body = string.IsNullOrWhiteSpace(route.MessageTemplate)
                    ? defaultBody
                    : ApplyTemplate(route.MessageTemplate, defaultBody);

                var thread = await messageRepository.GetOrCreateThreadAsync(
                    route.Channel,
                    route.Provider,
                    route.IntegrationFeedId,
                    deviceId,
                    assetId,
                    route.ExternalPeerId ?? route.Recipient ?? route.Name,
                    route.DisplayName ?? route.Name,
                    JsonSerializer.Serialize(new { routeId = route.Id, alertType = eventType }),
                    cancellationToken);

                var message = await messageRepository.AddMessageAsync(new MessageEntry
                {
                    ThreadId = thread.Id,
                    Direction = MessageDirection.Outbound,
                    Status = MessageStatus.Queued,
                    Recipient = route.Recipient ?? route.ExternalPeerId,
                    Body = body,
                    Metadata = JsonSerializer.Serialize(new { routeId = route.Id, alert = metadata }),
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                broadcaster.Publish(new LiveEvent(LiveEventType.Message, new
                {
                    id = message.Id,
                    threadId = message.ThreadId,
                    direction = message.Direction,
                    status = message.Status,
                    body = message.Body,
                    createdAt = ApiDateTime.Utc(message.CreatedAt)
                }));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alert route {RouteId} failed for {EventType}.", route.Id, eventType);
            }
        }
    }

    private static string BuildSpeedAlertBody(SpeedAlert alert)
    {
        var subject = alert.Asset?.Name ?? alert.Device?.Label ?? alert.Device?.Identifier ?? alert.DeviceId.ToString();
        return $"Speed alert for {subject}: {alert.ObservedSpeedKmh:0.#} km/h exceeded {alert.ThresholdKmh:0.#} km/h.";
    }

    private static string BuildGeofenceBreachBody(GeofenceBreach breach)
    {
        var subject = breach.Asset?.Name ?? breach.Device?.Label ?? breach.Device?.Identifier ?? breach.DeviceId.ToString();
        var geofence = breach.Geofence?.Name ?? "geofence";
        return $"Geofence {breach.EventType.ToString().ToLowerInvariant()} for {subject}: {geofence}.";
    }

    private static string ApplyTemplate(string template, string defaultBody)
        => template.Replace("{message}", defaultBody, StringComparison.OrdinalIgnoreCase);
}
