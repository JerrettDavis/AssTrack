using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class SensorReadingRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<SensorReading>> GetRecentAsync(
        Guid? assetId = null,
        Guid? deviceId = null,
        string? sensorType = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = Query();

        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId.Value);
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
        if (!string.IsNullOrWhiteSpace(sensorType)) query = query.Where(x => x.SensorType == sensorType.Trim().ToLowerInvariant());
        if (from.HasValue) query = query.Where(x => x.ObservedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.ObservedAt <= to.Value);

        return await query
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SensorReading>> GetLatestByAssetAsync(Guid assetId, int limit = 12, CancellationToken cancellationToken = default)
        => await Query()
            .Where(x => x.AssetId == assetId)
            .GroupBy(x => x.SensorType)
            .Select(group => group.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.ReceivedAt).First())
            .OrderBy(x => x.SensorType)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

    public async Task<SensorReading> AddAsync(SensorReading reading, CancellationToken cancellationToken = default)
    {
        dbContext.SensorReadings.Add(reading);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(reading.Id, cancellationToken) ?? reading;
    }

    public Task<SensorReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private IQueryable<SensorReading> Query()
        => dbContext.SensorReadings
            .Include(x => x.Asset)
            .Include(x => x.Device)
            .Include(x => x.IntegrationFeed);
}
