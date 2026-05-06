using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
        }).RequireAuthorization("Operator");

        geofences.MapPost(string.Empty, async ([FromBody] CreateGeofenceRequest request, GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["name"] = ["Name is required."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var validationErrors = ValidateShape(request.ShapeType, request.CenterLatitude, request.CenterLongitude, request.RadiusMeters, request.PolygonCoordinates);
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            var shapeType = NormalizeShapeType(request.ShapeType);
            var center = ResolveCenter(shapeType, request.CenterLatitude, request.CenterLongitude, request.PolygonCoordinates);

            var geofence = new Geofence
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                ShapeType = shapeType,
                CenterLatitude = center.Latitude,
                CenterLongitude = center.Longitude,
                RadiusMeters = shapeType == "polygon" ? 0 : request.RadiusMeters,
                PolygonJson = shapeType == "polygon" ? SerializePolygon(request.PolygonCoordinates) : null,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(geofence, cancellationToken);
            return Results.Created($"/api/geofences/{geofence.Id}", Map(geofence));
        }).RequireAuthorization("Operator");

        geofences.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateGeofenceRequest request, GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["name"] = ["Name is required."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var validationErrors = ValidateShape(request.ShapeType, request.CenterLatitude, request.CenterLongitude, request.RadiusMeters, request.PolygonCoordinates);
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            var shapeType = NormalizeShapeType(request.ShapeType);
            var center = ResolveCenter(shapeType, request.CenterLatitude, request.CenterLongitude, request.PolygonCoordinates);

            var updated = await repository.UpdateAsync(
                id,
                request.Name.Trim(),
                request.Description,
                shapeType,
                center.Latitude,
                center.Longitude,
                shapeType == "polygon" ? 0 : request.RadiusMeters,
                shapeType == "polygon" ? SerializePolygon(request.PolygonCoordinates) : null,
                request.IsActive,
                cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        }).RequireAuthorization("Operator");

        geofences.MapDelete("/{id:guid}", async (Guid id, GeofenceRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Operator");

        geofences.MapGet("/breaches", async (
            GeofenceBreachRepository breachRepository,
            bool? unacknowledged,
            int? limit,
            DateTimeOffset? since,
            Guid? deviceId,
            Guid? assetId,
            Guid? geofenceId,
            int? page,
            int? pageSize,
            string? format,
            CancellationToken cancellationToken) =>
        {
            var sinceUtc = since?.UtcDateTime;
            
            // For pagination
            if (page.HasValue || pageSize.HasValue)
            {
                var pageNum = Math.Max(1, page ?? 1);
                var size = Math.Max(1, Math.Min(200, pageSize ?? 50));
                
                var (items, totalCount) = await breachRepository.GetRecentPagedAsync(
                    pageNum,
                    size,
                    unacknowledged,
                    sinceUtc,
                    deviceId,
                    assetId,
                    geofenceId,
                    cancellationToken);
                
                return Results.Ok(new AssTrack.Domain.Contracts.PagedResult<GeofenceBreachDto>(
                    items.Select(MapBreach).ToList(),
                    totalCount,
                    pageNum,
                    size));
            }
            
            // CSV export (unpaginated)
            if (format?.ToLowerInvariant() == "csv")
            {
                // CSV requires at least one filter
                if (!deviceId.HasValue && !assetId.HasValue && !geofenceId.HasValue && !since.HasValue && !unacknowledged.HasValue)
                {
                    var errors = new Dictionary<string, string[]>
                    {
                        ["filters"] = ["CSV export requires at least one filter parameter (deviceId, assetId, geofenceId, since, or unacknowledged)."]
                    };
                    return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                
                var items = await breachRepository.GetRecentAsync(
                    limit ?? 100, 
                    unacknowledged, 
                    sinceUtc, 
                    deviceId, 
                    assetId, 
                    geofenceId,
                    cancellationToken);
                
                var csv = BuildGeofenceBreachCsv(items);
                return Results.Content(csv, "text/csv", System.Text.Encoding.UTF8);
            }
            
            // Default unpaginated response
            var defaultItems = await breachRepository.GetRecentAsync(
                limit ?? 100, 
                unacknowledged, 
                sinceUtc, 
                deviceId, 
                assetId, 
                geofenceId,
                cancellationToken);
            
            return Results.Ok(defaultItems.Select(MapBreach));
        }).RequireAuthorization("Operator");

        geofences.MapPost("/breaches/bulk-acknowledge", async (BulkAcknowledgeBreachesRequest request, GeofenceBreachRepository breachRepository, CancellationToken cancellationToken) =>
        {
            var count = await breachRepository.BulkAcknowledgeAsync(request.Ids, DateTime.UtcNow, request.AcknowledgedBy, cancellationToken);
            return Results.Ok(new { count });
        }).RequireAuthorization("Operator");

        geofences.MapPost("/breaches/{id:guid}/acknowledge", async (Guid id, AcknowledgeBreachRequest request, GeofenceBreachRepository breachRepository, CancellationToken cancellationToken) =>
        {
            var updated = await breachRepository.AcknowledgeAsync(id, DateTime.UtcNow, request.AcknowledgedBy, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(MapBreach(updated));
        }).RequireAuthorization("Operator");

        return group;
    }

    internal static GeofenceDto Map(Geofence geofence) => new(
        geofence.Id,
        geofence.Name,
        geofence.Description,
        geofence.ShapeType,
        geofence.CenterLatitude,
        geofence.CenterLongitude,
        geofence.RadiusMeters,
        GeofenceEvaluator.ParsePolygon(geofence.PolygonJson)
            .Select(point => new GeofencePointDto(point.Latitude, point.Longitude))
            .ToList(),
        geofence.IsActive,
        geofence.CreatedAt,
        geofence.IsSeeded);

    private static string NormalizeShapeType(string? shapeType)
        => string.Equals(shapeType, "polygon", StringComparison.OrdinalIgnoreCase) ? "polygon" : "circle";

    private static Dictionary<string, string[]> ValidateShape(
        string? rawShapeType,
        double centerLatitude,
        double centerLongitude,
        double radiusMeters,
        IReadOnlyList<GeofencePointDto>? polygonCoordinates)
    {
        var validationErrors = new Dictionary<string, string[]>();
        var shapeType = NormalizeShapeType(rawShapeType);

        if (shapeType == "circle")
        {
            if (radiusMeters <= 0)
                validationErrors["radiusMeters"] = ["Radius must be greater than 0."];
            if (centerLatitude < -90 || centerLatitude > 90)
                validationErrors["centerLatitude"] = ["CenterLatitude must be between -90 and 90."];
            if (centerLongitude < -180 || centerLongitude > 180)
                validationErrors["centerLongitude"] = ["CenterLongitude must be between -180 and 180."];
            return validationErrors;
        }

        var points = polygonCoordinates ?? [];
        if (points.Count < 3)
            validationErrors["polygonCoordinates"] = ["A freeform geofence requires at least three points."];

        if (points.Any(point => point.Latitude < -90 || point.Latitude > 90))
            validationErrors["polygonCoordinates.latitude"] = ["Polygon latitudes must be between -90 and 90."];

        if (points.Any(point => point.Longitude < -180 || point.Longitude > 180))
            validationErrors["polygonCoordinates.longitude"] = ["Polygon longitudes must be between -180 and 180."];

        return validationErrors;
    }

    private static GeofencePointDto ResolveCenter(string shapeType, double centerLatitude, double centerLongitude, IReadOnlyList<GeofencePointDto>? polygonCoordinates)
    {
        if (shapeType == "circle" || polygonCoordinates is null || polygonCoordinates.Count == 0)
            return new GeofencePointDto(centerLatitude, centerLongitude);

        return new GeofencePointDto(
            polygonCoordinates.Average(point => point.Latitude),
            polygonCoordinates.Average(point => point.Longitude));
    }

    private static string? SerializePolygon(IReadOnlyList<GeofencePointDto>? polygonCoordinates)
        => polygonCoordinates is null
            ? null
            : JsonSerializer.Serialize(polygonCoordinates.Select(point => new GeofenceVertex(point.Latitude, point.Longitude)), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    internal static GeofenceBreachDto MapBreach(GeofenceBreach breach) => new(
        breach.Id,
        breach.ObservationId,
        breach.GeofenceId,
        breach.Geofence.Name,
        breach.DeviceId,
        breach.Device?.Identifier,
        breach.Asset?.Name,
        breach.AssetId,
        breach.EventType.ToString(),
        breach.DetectedAt,
        breach.AcknowledgedAtUtc,
        breach.AcknowledgedBy);

    private static string BuildGeofenceBreachCsv(IReadOnlyList<GeofenceBreach> breaches)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,ObservationId,GeofenceId,GeofenceName,DeviceId,DeviceIdentifier,AssetId,AssetName,EventType,DetectedAt,AcknowledgedAtUtc,AcknowledgedBy");
        
        foreach (var breach in breaches)
        {
            sb.AppendLine($"{breach.Id},{breach.ObservationId},{breach.GeofenceId},{CsvEscape(breach.Geofence?.Name)},{breach.DeviceId},{CsvEscape(breach.Device?.Identifier)},{breach.AssetId},{CsvEscape(breach.Asset?.Name)},{CsvEscape(breach.EventType.ToString())},{breach.DetectedAt:O},{breach.AcknowledgedAtUtc?.ToString("O")},{CsvEscape(breach.AcknowledgedBy)}");
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

