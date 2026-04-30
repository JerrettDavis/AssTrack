using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class ObservationRepository(AssTrackDbContext dbContext)
{
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
        => dbContext.Observations
            .Include(x => x.Device)
            .ThenInclude(x => x.Asset)
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Observation>> GetLatestPerDeviceAsync(CancellationToken cancellationToken = default)
    {
        var latestIds = dbContext.Observations
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
}
