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
}
