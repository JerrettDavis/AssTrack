using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using AssTrack.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Api.Services;

public sealed class ObservationIngestService(
    DeviceRepository deviceRepository,
    ObservationRepository observationRepository,
    GeofenceRepository geofenceRepository,
    GeofenceBreachRepository geofenceBreachRepository,
    SpeedAlertRepository speedAlertRepository,
    IWebhookNotificationService webhookService,
    ILiveEventBroadcaster broadcaster) : IObservationIngestService
{
    public async Task<IngestResult> IngestAsync(CreateObservationRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = new Dictionary<string, string[]>();
        if (request.Latitude < -90 || request.Latitude > 90)
            validationErrors["latitude"] = ["Latitude must be between -90 and 90."];
        if (request.Longitude < -180 || request.Longitude > 180)
            validationErrors["longitude"] = ["Longitude must be between -180 and 180."];
        if (request.SpeedKmh < 0 || request.SpeedKmh > 5000)
            validationErrors["speedKmh"] = ["Speed must be between 0 and 5000 km/h."];
        if (request.ObservedAt > DateTime.UtcNow.AddMinutes(5))
            validationErrors["observedAt"] = ["ObservedAt cannot be more than 5 minutes in the future."];
        if (validationErrors.Count > 0)
            throw new ObservationIngestException(validationErrors);

        if (request.DeviceId == Guid.Empty && string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            throw new ObservationIngestException(
                new Dictionary<string, string[]> { ["deviceId"] = ["Either DeviceId or DeviceIdentifier must be provided."] });
        }

        var device = request.DeviceId != Guid.Empty
            ? await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            : null;

        if (device is null && !string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            device = await deviceRepository.GetByIdentifierAsync(request.DeviceIdentifier.Trim(), cancellationToken);
        }

        if (device is null)
        {
            throw new ObservationIngestException(
                new Dictionary<string, string[]> { ["deviceId"] = ["Device was not found."] });
        }

        var existing = await observationRepository.GetByDeviceAndTimeAsync(device.Id, request.ObservedAt, cancellationToken);
        if (existing is not null)
        {
            return new IngestResult(existing, IsDuplicate: true, null, []);
        }

        var observation = new Observation
        {
            DeviceId = device.Id,
            ObservedAt = request.ObservedAt,
            ReceivedAt = DateTime.UtcNow,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Altitude = request.Altitude,
            AccuracyMeters = request.AccuracyMeters,
            SpeedKmh = request.SpeedKmh,
            HeadingDegrees = request.HeadingDegrees,
            Metadata = request.Metadata
        };

        Observation created;
        try
        {
            created = await observationRepository.AddAsync(observation, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            var existingOnConflict = await observationRepository.GetByDeviceAndTimeAsync(device.Id, request.ObservedAt, cancellationToken);
            if (existingOnConflict is not null)
                return new IngestResult(existingOnConflict, IsDuplicate: true, null, []);
            throw;
        }

        broadcaster.Publish(new LiveEvent(LiveEventType.Observation, new {
            id = created.Id,
            deviceId = created.DeviceId,
            assetId = device.AssetId,
            latitude = created.Latitude,
            longitude = created.Longitude,
            speedKmh = created.SpeedKmh,
            observedAt = created.ObservedAt
        }));

        SpeedAlert? firedAlert = null;
        var alert = SpeedAlertEvaluator.Evaluate(created, device.AssetId, device.Asset?.SpeedThresholdKmh ?? SpeedAlertEvaluator.DefaultThresholdKmh);
        if (alert is not null)
        {
            var hasCooldown = await speedAlertRepository.HasRecentAlertAsync(device.Id, SpeedAlertEvaluator.AlertCooldown, cancellationToken);
            if (!hasCooldown)
            {
                await observationRepository.AddSpeedAlertAsync(alert, cancellationToken);
                alert.Device = device;
                alert.Asset = device.Asset;
                await webhookService.NotifySpeedAlertAsync(alert, cancellationToken);
                firedAlert = alert;
                broadcaster.Publish(new LiveEvent(LiveEventType.SpeedAlert, new {
                    id = alert.Id,
                    deviceId = device.Id,
                    assetId = device.AssetId,
                    observedSpeedKmh = alert.ObservedSpeedKmh,
                    thresholdKmh = alert.ThresholdKmh,
                    triggeredAt = alert.TriggeredAt
                }));
            }
        }

        var activeGeofences = await geofenceRepository.GetActiveAsync(cancellationToken);
        var firedBreaches = new List<GeofenceBreach>();
        foreach (var geofence in activeGeofences)
        {
            var isInside = GeofenceEvaluator.IsInside(geofence, created);
            var state = await geofenceBreachRepository.GetStateAsync(device.Id, geofence.Id, cancellationToken);
            if (state is not null && created.ObservedAt < state.LastObservationAt) continue;
            var wasInside = state?.IsInside ?? false;

            if (isInside && !wasInside)
            {
                var breach = new GeofenceBreach
                {
                    ObservationId = created.Id,
                    GeofenceId = geofence.Id,
                    DeviceId = device.Id,
                    AssetId = device.AssetId,
                    DetectedAt = DateTime.UtcNow,
                    EventType = GeofenceBreachEventType.Enter
                };
                await geofenceBreachRepository.AddAsync(breach, cancellationToken);
                breach.Device = device;
                breach.Asset = device.Asset;
                breach.Geofence = geofence;
                await webhookService.NotifyGeofenceBreachAsync(breach, cancellationToken);
                firedBreaches.Add(breach);
                broadcaster.Publish(new LiveEvent(LiveEventType.GeofenceBreach, new {
                    id = breach.Id,
                    deviceId = device.Id,
                    assetId = device.AssetId,
                    geofenceId = geofence.Id,
                    eventType = breach.EventType.ToString(),
                    detectedAt = breach.DetectedAt
                }));
            }
            else if (!isInside && wasInside)
            {
                var breach = new GeofenceBreach
                {
                    ObservationId = created.Id,
                    GeofenceId = geofence.Id,
                    DeviceId = device.Id,
                    AssetId = device.AssetId,
                    DetectedAt = DateTime.UtcNow,
                    EventType = GeofenceBreachEventType.Exit
                };
                await geofenceBreachRepository.AddAsync(breach, cancellationToken);
                breach.Device = device;
                breach.Asset = device.Asset;
                breach.Geofence = geofence;
                await webhookService.NotifyGeofenceBreachAsync(breach, cancellationToken);
                firedBreaches.Add(breach);
                broadcaster.Publish(new LiveEvent(LiveEventType.GeofenceBreach, new {
                    id = breach.Id,
                    deviceId = device.Id,
                    assetId = device.AssetId,
                    geofenceId = geofence.Id,
                    eventType = breach.EventType.ToString(),
                    detectedAt = breach.DetectedAt
                }));
            }

            await geofenceBreachRepository.UpsertStateAsync(new DeviceGeofenceState
            {
                DeviceId = device.Id,
                GeofenceId = geofence.Id,
                IsInside = isInside,
                LastObservationAt = created.ObservedAt,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        created = await observationRepository.GetByIdAsync(created.Id, cancellationToken) ?? created;
        return new IngestResult(created, IsDuplicate: false, firedAlert, firedBreaches);
    }
}
