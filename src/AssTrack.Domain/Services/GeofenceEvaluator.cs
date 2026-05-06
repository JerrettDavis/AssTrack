using AssTrack.Domain.Models;
using System.Text.Json;

namespace AssTrack.Domain.Services;

public static class GeofenceEvaluator
{
    private const double EarthRadiusMeters = 6_371_000;

    public static bool IsInside(Geofence geofence, Observation observation)
    {
        if (string.Equals(geofence.ShapeType, "polygon", StringComparison.OrdinalIgnoreCase))
        {
            var points = ParsePolygon(geofence.PolygonJson);
            return points.Count >= 3 && IsInsidePolygon(points, observation.Latitude, observation.Longitude);
        }

        return HaversineDistance(geofence.CenterLatitude, geofence.CenterLongitude, observation.Latitude, observation.Longitude) <= geofence.RadiusMeters;
    }

    public static IReadOnlyList<GeofenceVertex> ParsePolygon(string? polygonJson)
    {
        if (string.IsNullOrWhiteSpace(polygonJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<GeofenceVertex>>(polygonJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsInsidePolygon(IReadOnlyList<GeofenceVertex> polygon, double latitude, double longitude)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var yi = polygon[i].Latitude;
            var xi = polygon[i].Longitude;
            var yj = polygon[j].Latitude;
            var xj = polygon[j].Longitude;
            var intersects = ((yi > latitude) != (yj > latitude)) &&
                (longitude < (xj - xi) * (latitude - yi) / ((yj - yi) == 0 ? double.Epsilon : yj - yi) + xi);
            if (intersects) inside = !inside;
        }

        return inside;
    }

    public static double HaversineDistance(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var dLat = ToRad(latitude2 - latitude1);
        var dLon = ToRad(longitude2 - longitude1);
        var lat1 = ToRad(latitude1);
        var lat2 = ToRad(latitude2);

        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static double ToRad(double value) => value * Math.PI / 180d;
}

public sealed record GeofenceVertex(double Latitude, double Longitude);
