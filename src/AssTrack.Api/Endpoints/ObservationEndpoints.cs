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
            [FromBody] CreateObservationBody request,
            DeviceRepository deviceRepository,
            ObservationRepository observationRepository,
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
            var alert = SpeedAlertEvaluator.Evaluate(created, device.AssetId);
            if (alert is not null)
            {
                await observationRepository.AddSpeedAlertAsync(alert, cancellationToken);
            }

            created = await observationRepository.GetByIdAsync(created.Id, cancellationToken) ?? created;
            return Results.Created($"/api/observations/{created.Id}", Map(created));
        }

        observations.MapPost(string.Empty, HandleIngest);
        observations.MapPost("/ingest", HandleIngest);

        return group;
    }

    public sealed class CreateObservationBody
    {
        public Guid DeviceId { get; init; }
        public string? DeviceIdentifier { get; init; }
        public DateTime ObservedAt { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double? Altitude { get; init; }
        public double? AccuracyMeters { get; init; }
        public double? SpeedKmh { get; init; }
        public double? HeadingDegrees { get; init; }
        public string? Metadata { get; init; }
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
