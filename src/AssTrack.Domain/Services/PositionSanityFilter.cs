using AssTrack.Domain.Models;

namespace AssTrack.Domain.Services;

public static class PositionSanityFilter
{
    private const double EarthRadiusMeters = 6_371_000d;
    private const double NullIslandRadiusMeters = 10_000d;

    public static bool IsNullIslandNoise(double latitude, double longitude)
        => DistanceMeters(latitude, longitude, 0, 0) <= NullIslandRadiusMeters;

    public static bool IsPlausible(double latitude, double longitude)
        => latitude is >= -90 and <= 90 &&
           longitude is >= -180 and <= 180 &&
           !IsNullIslandNoise(latitude, longitude);

    public static bool IsPlausible(Observation observation)
        => IsPlausible(observation.Latitude, observation.Longitude);

    private static double DistanceMeters(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        var dLat = DegreesToRadians(toLatitude - fromLatitude);
        var dLon = DegreesToRadians(toLongitude - fromLongitude);
        var lat1 = DegreesToRadians(fromLatitude);
        var lat2 = DegreesToRadians(toLatitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
