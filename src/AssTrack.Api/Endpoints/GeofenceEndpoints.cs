using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

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

        geofences.MapPost(string.Empty, async (CreateGeofenceRequest request, GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

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
