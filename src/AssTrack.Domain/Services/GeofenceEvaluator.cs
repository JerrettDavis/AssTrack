using AssTrack.Domain.Models;

namespace AssTrack.Domain.Services;

public static class GeofenceEvaluator
{
    private const double EarthRadiusMeters = 6_371_000;

    public static bool IsInside(Geofence geofence, Observation observation)
        => HaversineDistance(geofence.CenterLatitude, geofence.CenterLongitude, observation.Latitude, observation.Longitude) <= geofence.RadiusMeters;

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
