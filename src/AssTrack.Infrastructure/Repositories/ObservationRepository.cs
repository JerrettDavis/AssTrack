using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class ObservationRepository(AssTrackDbContext dbContext)
{
    private const double NullIslandDegrees = 0.09009009d;

    public async Task<Observation> AddAsync(Observation observation, CancellationToken cancellationToken = default)
    {
        dbContext.Observations.Add(observation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(observation.Id, cancellationToken) ?? observation;
    }

    public async Task<SpeedAlert> AddSpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default)
    {
        dbContext.SpeedAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public Task<Observation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Observation?> GetByDeviceAndTimeAsync(Guid deviceId, DateTime observedAt, CancellationToken cancellationToken = default)
        => dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.ObservedAt == observedAt, cancellationToken);

    public async Task<IReadOnlyList<Observation>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default)
        => await dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .OrderByDescending(x => x.ObservedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<Observation?> GetLatestForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => PlausibleObservations()
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Observation>> GetLatestPerDeviceAsync(CancellationToken cancellationToken = default)
    {
        var latestIds = PlausibleObservations()
            .GroupBy(o => o.DeviceId)
            .Select(g => g.OrderByDescending(o => o.ObservedAt).ThenByDescending(o => o.ReceivedAt).Select(o => o.Id).First());

        return await dbContext.Observations
            .Where(o => latestIds.Contains(o.Id))
            .Include(o => o.Device)
            .ThenInclude(d => d.Asset)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Observation> Items, int TotalCount)> GetHistoryAsync(
        Guid? deviceId = null,
        Guid? assetId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .AsQueryable();

        if (deviceId.HasValue)
            query = query.Where(x => x.DeviceId == deviceId.Value);

        if (assetId.HasValue)
            query = query.Where(x => x.Device.AssetId == assetId.Value);

        if (from.HasValue)
            query = query.Where(x => x.ObservedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.ObservedAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ObservedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Observation> Items, IReadOnlyList<ObservationTimelineBucket> Buckets, int TotalCount, bool Truncated)> GetTimelineAsync(
        Guid? deviceId,
        Guid? assetId,
        DateTime from,
        DateTime to,
        int bucketMinutes,
        int maxPoints,
        CancellationToken cancellationToken = default)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);
        var boundedBucketMinutes = Math.Clamp(bucketMinutes, 1, 24 * 60);
        var boundedMaxPoints = Math.Clamp(maxPoints, 100, 10_000);

        var query = PlausibleObservations()
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .Where(x => x.ObservedAt >= from && x.ObservedAt <= to);

        if (deviceId.HasValue)
            query = query.Where(x => x.DeviceId == deviceId.Value);

        if (assetId.HasValue)
            query = query.Where(x => x.Device.AssetId == assetId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var bucketSizeTicks = TimeSpan.FromMinutes(boundedBucketMinutes).Ticks;
        var bucketSeed = from.Ticks;

        var buckets = await query
            .GroupBy(x => (x.ObservedAt.Ticks - bucketSeed) / bucketSizeTicks)
            .Select(group => new
            {
                Index = group.Key,
                Count = group.Count()
            })
            .OrderBy(x => x.Index)
            .ToListAsync(cancellationToken);

        var cappedItems = await query
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.ReceivedAt)
            .Take(boundedMaxPoints)
            .ToListAsync(cancellationToken);

        var items = cappedItems
            .OrderBy(x => x.ObservedAt)
            .ThenBy(x => x.ReceivedAt)
            .ToList();

        return (
            items,
            buckets.Select(bucket =>
            {
                var start = new DateTime(bucketSeed + bucket.Index * bucketSizeTicks, DateTimeKind.Utc);
                var end = start.AddMinutes(boundedBucketMinutes);
                return new ObservationTimelineBucket(start, end > to ? to : end, bucket.Count);
            }).ToList(),
            totalCount,
            totalCount > boundedMaxPoints);
    }

    private IQueryable<Observation> PlausibleObservations()
        => dbContext.Observations.Where(o => !(
            o.Latitude >= -NullIslandDegrees &&
            o.Latitude <= NullIslandDegrees &&
            o.Longitude >= -NullIslandDegrees &&
            o.Longitude <= NullIslandDegrees));

    public async Task<(int MatchingObservations, int DeletedObservations, int AffectedDevices, int ResetGeofenceStates)> DeleteNullIslandNoiseAsync(
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var noiseQuery = NullIslandNoiseQuery();
        var matchingObservations = await noiseQuery.CountAsync(cancellationToken);
        var affectedDeviceIds = await noiseQuery
            .Select(o => o.DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var resetStateCount = await dbContext.DeviceGeofenceStates
            .Where(x => affectedDeviceIds.Contains(x.DeviceId))
            .CountAsync(cancellationToken);

        if (dryRun || matchingObservations == 0)
        {
            return (matchingObservations, 0, affectedDeviceIds.Count, dryRun ? resetStateCount : 0);
        }

        var resetStates = await dbContext.DeviceGeofenceStates
            .Where(x => affectedDeviceIds.Contains(x.DeviceId))
            .ExecuteDeleteAsync(cancellationToken);

        var deleted = await NullIslandNoiseQuery().ExecuteDeleteAsync(cancellationToken);

        return (matchingObservations, deleted, affectedDeviceIds.Count, resetStates);
    }

    private IQueryable<Observation> NullIslandNoiseQuery()
        => dbContext.Observations.Where(o =>
            o.Latitude >= -NullIslandDegrees &&
            o.Latitude <= NullIslandDegrees &&
            o.Longitude >= -NullIslandDegrees &&
            o.Longitude <= NullIslandDegrees);
}

public sealed record ObservationTimelineBucket(DateTime Start, DateTime End, int Count);
