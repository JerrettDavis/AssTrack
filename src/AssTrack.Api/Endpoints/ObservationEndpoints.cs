using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

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

            var created = await observationRepository.AddAsync(observation, cancellationToken);
            var alert = SpeedAlertEvaluator.Evaluate(created, device.AssetId, device.Asset?.SpeedThresholdKmh ?? SpeedAlertEvaluator.DefaultThresholdKmh);
            if (alert is not null)
            {
                var hasCooldown = await speedAlertRepository.HasRecentAlertAsync(device.Id, SpeedAlertEvaluator.AlertCooldown, cancellationToken);
                if (!hasCooldown)
                {
                    await observationRepository.AddSpeedAlertAsync(alert, cancellationToken);
                }
            }

            var activeGeofences = await geofenceRepository.GetActiveAsync(cancellationToken);
            foreach (var geofence in activeGeofences)
            {
                var isInside = GeofenceEvaluator.IsInside(geofence, created);
                var state = await geofenceBreachRepository.GetStateAsync(device.Id, geofence.Id, cancellationToken);
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
                }

                await geofenceBreachRepository.UpsertStateAsync(new DeviceGeofenceState
                {
                    DeviceId = device.Id,
                    GeofenceId = geofence.Id,
                    IsInside = isInside,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }

            created = await observationRepository.GetByIdAsync(created.Id, cancellationToken) ?? created;
            return Results.Created($"/api/observations/{created.Id}", Map(created));
        }

        observations.MapPost(string.Empty, HandleIngest);
        observations.MapPost("/ingest", HandleIngest);

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
}

