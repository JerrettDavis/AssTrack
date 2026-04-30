using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Api.Endpoints;

public static class ObservationEndpoints
{
    public static RouteGroupBuilder MapObservationEndpoints(this RouteGroupBuilder group)
    {
        var observations = group.MapGroup("/observations");

        observations.MapGet(string.Empty, async (ObservationRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetRecentAsync(cancellationToken: cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        observations.MapGet("/latest/{deviceId:guid}", async (Guid deviceId, ObservationRepository repository, CancellationToken cancellationToken) =>
        {
            var observation = await repository.GetLatestForDeviceAsync(deviceId, cancellationToken);
            return observation is null ? Results.NotFound() : Results.Ok(Map(observation));
        });

        observations.MapGet("/latest-positions", async (ObservationRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetLatestPerDeviceAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        static async Task<IResult> HandleIngest(
            [FromBody] CreateObservationRequest request,
            DeviceRepository deviceRepository,
            ObservationRepository observationRepository,
            GeofenceRepository geofenceRepository,
            GeofenceBreachRepository geofenceBreachRepository,
            SpeedAlertRepository speedAlertRepository,
            IWebhookNotificationService webhookService,
            CancellationToken cancellationToken)
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
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);

            if (request.DeviceId == Guid.Empty && string.IsNullOrWhiteSpace(request.DeviceIdentifier))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["deviceId"] = ["Either DeviceId or DeviceIdentifier must be provided."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
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
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["deviceId"] = ["Device was not found."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var existing = await observationRepository.GetByDeviceAndTimeAsync(device.Id, request.ObservedAt, cancellationToken);
            if (existing is not null)
            {
                return Results.Ok(Map(existing));
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
                    return Results.Ok(Map(existingOnConflict));
                throw;
            }
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
                }
            }

            var activeGeofences = await geofenceRepository.GetActiveAsync(cancellationToken);
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
            return Results.Created($"/api/observations/{created.Id}", Map(created));
        }

        observations.MapPost(string.Empty, HandleIngest).RequireRateLimiting("ingest");
        observations.MapPost("/ingest", HandleIngest).RequireRateLimiting("ingest");

        observations.MapGet("/history", async (
            ObservationRepository observationRepository,
            Guid? deviceId,
            Guid? assetId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            string? format,
            CancellationToken cancellationToken) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            
            // For CSV export
            if (format?.ToLowerInvariant() == "csv")
            {
                // CSV requires at least one filter
                if (!deviceId.HasValue && !assetId.HasValue && !from.HasValue && !to.HasValue)
                {
                    var errors = new Dictionary<string, string[]>
                    {
                        ["filters"] = ["CSV export requires at least one filter parameter (deviceId, assetId, from, or to)."]
                    };
                    return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                
                // Cap at 5000 rows and reset page to 1
                ps = Math.Min(ps, 5000);
                p = 1;
                
                var (items, _) = await observationRepository.GetHistoryAsync(
                    deviceId, 
                    assetId, 
                    from?.UtcDateTime, 
                    to?.UtcDateTime, 
                    p, 
                    ps, 
                    cancellationToken);
                
                var csv = BuildObservationCsv(items);
                return Results.Content(csv, "text/csv", System.Text.Encoding.UTF8);
            }
            
            // JSON response
            var (observations, totalCount) = await observationRepository.GetHistoryAsync(
                deviceId, 
                assetId, 
                from?.UtcDateTime, 
                to?.UtcDateTime, 
                p, 
                ps, 
                cancellationToken);
            
            var result = new PagedResult<ObservationDto>(
                observations.Select(Map).ToList(),
                totalCount,
                p,
                ps
            );
            
            return Results.Ok(result);
        });

        return group;
    }

    internal static ObservationDto Map(Observation observation) => new(
        observation.Id,
        observation.DeviceId,
        observation.Device.Identifier,
        observation.Device.AssetId,
        observation.Device.Asset?.Name,
        observation.ObservedAt,
        observation.ReceivedAt,
        observation.Latitude,
        observation.Longitude,
        observation.Altitude,
        observation.AccuracyMeters,
        observation.SpeedKmh,
        observation.HeadingDegrees,
        observation.Metadata);

    private static string BuildObservationCsv(IReadOnlyList<Observation> observations)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ObservationId,DeviceId,AssetId,ObservedAt,Latitude,Longitude,Altitude,SpeedKmh,Heading");
        
        foreach (var obs in observations)
        {
            sb.AppendLine($"{obs.Id},{obs.DeviceId},{obs.Device.AssetId},{obs.ObservedAt:O},{obs.Latitude},{obs.Longitude},{obs.Altitude},{obs.SpeedKmh},{obs.HeadingDegrees}");
        }
        
        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        
        return value;
    }
}

