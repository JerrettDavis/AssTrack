using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AssTrack.Api.Endpoints;

public static class GeofenceEndpoints
{
    public static RouteGroupBuilder MapGeofenceEndpoints(this RouteGroupBuilder group)
    {
        var geofences = group.MapGroup("/geofences");

        geofences.MapGet(string.Empty, async (GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        geofences.MapPost(string.Empty, async ([FromBody] CreateGeofenceBody request, GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["name"] = ["Name is required."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var validationErrors = new Dictionary<string, string[]>();
            if (request.RadiusMeters <= 0)
                validationErrors["radiusMeters"] = ["Radius must be greater than 0."];
            if (request.CenterLatitude < -90 || request.CenterLatitude > 90)
                validationErrors["centerLatitude"] = ["CenterLatitude must be between -90 and 90."];
            if (request.CenterLongitude < -180 || request.CenterLongitude > 180)
                validationErrors["centerLongitude"] = ["CenterLongitude must be between -180 and 180."];
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);

            var geofence = new Geofence
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                CenterLatitude = request.CenterLatitude,
                CenterLongitude = request.CenterLongitude,
                RadiusMeters = request.RadiusMeters,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(geofence, cancellationToken);
            return Results.Created($"/api/geofences/{geofence.Id}", Map(geofence));
        });

        return group;
    }

    public sealed class CreateGeofenceBody
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public double CenterLatitude { get; init; }
        public double CenterLongitude { get; init; }
        public double RadiusMeters { get; init; }
        public bool? IsActive { get; init; }
    }

    internal static GeofenceDto Map(Geofence geofence) => new(
        geofence.Id,
        geofence.Name,
        geofence.Description,
        geofence.CenterLatitude,
        geofence.CenterLongitude,
        geofence.RadiusMeters,
        geofence.IsActive,
        geofence.CreatedAt);
}
