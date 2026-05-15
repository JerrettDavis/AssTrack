using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class ReportRepository(AssTrackDbContext dbContext)
{
    private const double MovingSpeedThresholdKmh = 5d;
    private const double MaxPlausibleSegmentSpeedKmh = 500d;
    private const double EarthRadiusKm = 6371.0088d;

    public async Task<UtilizationReportDto> GetUtilizationAsync(
        DateTime from,
        DateTime to,
        Guid? assetId = null,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .Where(x => x.ObservedAt >= from && x.ObservedAt <= to);

        if (assetId.HasValue)
            query = query.Where(x => x.Device.AssetId == assetId.Value);

        if (deviceId.HasValue)
            query = query.Where(x => x.DeviceId == deviceId.Value);

        var observations = await query
            .OrderBy(x => x.DeviceId)
            .ThenBy(x => x.ObservedAt)
            .ThenBy(x => x.ReceivedAt)
            .ToListAsync(cancellationToken);

        var items = observations
            .GroupBy(x => x.DeviceId)
            .Select(group => BuildItem(group.ToList()))
            .OrderByDescending(x => x.DistanceKm)
            .ThenBy(x => x.AssetName ?? x.DeviceIdentifier)
            .ToList();

        return new UtilizationReportDto(
            ApiUtc(from),
            ApiUtc(to),
            ApiUtc(DateTime.UtcNow),
            items.Select(x => x.AssetId).Where(x => x.HasValue).Distinct().Count(),
            items.Count,
            observations.Count,
            Math.Round(items.Sum(x => x.DistanceKm), 2),
            Math.Round(items.Sum(x => x.MovingMinutes), 1),
            Math.Round(items.Sum(x => x.IdleMinutes), 1),
            items);
    }

    private static UtilizationReportItemDto BuildItem(IReadOnlyList<Observation> observations)
    {
        var first = observations[0];
        var distanceKm = 0d;
        var movingMinutes = 0d;
        var idleMinutes = 0d;
        var stopCount = 0;
        var wasMoving = false;
        var hasState = false;

        for (var i = 1; i < observations.Count; i++)
        {
            var previous = observations[i - 1];
            var current = observations[i];
            var elapsedMinutes = (current.ObservedAt - previous.ObservedAt).TotalMinutes;
            if (elapsedMinutes <= 0)
                continue;

            var segmentDistanceKm = DistanceKm(previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);
            var calculatedSpeedKmh = segmentDistanceKm / (elapsedMinutes / 60d);
            if (calculatedSpeedKmh > MaxPlausibleSegmentSpeedKmh)
                segmentDistanceKm = 0d;

            var segmentSpeedKmh = current.SpeedKmh ?? calculatedSpeedKmh;
            var isMoving = segmentSpeedKmh >= MovingSpeedThresholdKmh && segmentDistanceKm > 0;

            distanceKm += segmentDistanceKm;
            if (isMoving)
            {
                movingMinutes += elapsedMinutes;
            }
            else
            {
                idleMinutes += elapsedMinutes;
                if (hasState && wasMoving)
                    stopCount++;
            }

            wasMoving = isMoving;
            hasState = true;
        }

        var maxSpeed = observations
            .Select(x => x.SpeedKmh)
            .Where(x => x.HasValue)
            .Max();

        var averageMovingSpeed = movingMinutes > 0
            ? distanceKm / (movingMinutes / 60d)
            : (double?)null;

        return new UtilizationReportItemDto(
            first.DeviceId,
            first.Device.Identifier,
            first.Device.AssetId,
            first.Device.Asset?.Name,
            ApiUtc(observations.First().ObservedAt),
            ApiUtc(observations[^1].ObservedAt),
            observations.Count,
            Math.Round(distanceKm, 2),
            Math.Round(movingMinutes, 1),
            Math.Round(idleMinutes, 1),
            stopCount,
            maxSpeed.HasValue ? Math.Round(maxSpeed.Value, 1) : null,
            averageMovingSpeed.HasValue ? Math.Round(averageMovingSpeed.Value, 1) : null);
    }

    private static DateTime ApiUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var lat1 = ToRadians(latitude1);
        var lat2 = ToRadians(latitude2);
        var deltaLat = ToRadians(latitude2 - latitude1);
        var deltaLon = ToRadians(longitude2 - longitude1);

        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(deltaLon / 2d) * Math.Sin(deltaLon / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
