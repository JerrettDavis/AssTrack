using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
            IObservationIngestService ingestService,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await ingestService.IngestAsync(request, cancellationToken);
                if (result.IsDuplicate)
                    return Results.Ok(Map(result.Created!));
                return Results.Created($"/api/observations/{result.Created!.Id}", Map(result.Created!));
            }
            catch (ObservationIngestException ex)
            {
                return Results.ValidationProblem(ex.ValidationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        observations.MapPost(string.Empty, HandleIngest).RequireRateLimiting("ingest");
        observations.MapPost("/ingest", HandleIngest).RequireRateLimiting("ingest");

        observations.MapPost("/simulate", async (
            [FromBody] SimulateRequest request,
            ISimulationService simulationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await simulationService.SimulateAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (SimulationDisabledException)
            {
                return Results.Problem("Simulation is disabled in this environment.", statusCode: 403);
            }
        });

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

