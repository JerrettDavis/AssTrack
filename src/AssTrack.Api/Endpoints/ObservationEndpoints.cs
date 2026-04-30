using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using AssTrack.Infrastructure.Repositories;

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
            CreateObservationRequest request,
            DeviceRepository deviceRepository,
            ObservationRepository observationRepository,
            CancellationToken cancellationToken)
        {
            var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);
            if (device is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["deviceId"] = ["Device was not found."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var observation = new Observation
            {
                DeviceId = request.DeviceId,
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
