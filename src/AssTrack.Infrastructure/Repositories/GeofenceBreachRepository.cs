using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class GeofenceBreachRepository(AssTrackDbContext dbContext)
{
    public async Task<(IReadOnlyList<GeofenceBreach> items, int totalCount)> GetRecentPagedAsync(
        int page,
        int pageSize,
        bool? unacknowledgedOnly = null,
        DateTime? since = null,
        Guid? deviceId = null,
        Guid? assetId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GeofenceBreaches
            .Include(x => x.Geofence)
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .AsQueryable();
        if (unacknowledgedOnly == true)
            query = query.Where(x => x.AcknowledgedAtUtc == null);
        if (since.HasValue)
            query = query.Where(x => x.DetectedAt >= since.Value);
        if (deviceId.HasValue)
            query = query.Where(x => x.DeviceId == deviceId.Value);
        if (assetId.HasValue)
            query = query.Where(x => x.Device.AssetId == assetId.Value);
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(x => x.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<GeofenceBreach>> GetRecentAsync(
        int limit = 100,
        bool? unacknowledgedOnly = null,
        DateTime? since = null,
        Guid? deviceId = null,
        Guid? assetId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GeofenceBreaches
            .Include(x => x.Geofence)
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .AsQueryable();
        if (unacknowledgedOnly == true)
            query = query.Where(x => x.AcknowledgedAtUtc == null);
        if (since.HasValue)
            query = query.Where(x => x.DetectedAt >= since.Value);
        if (deviceId.HasValue)
            query = query.Where(x => x.DeviceId == deviceId.Value);
        if (assetId.HasValue)
            query = query.Where(x => x.Device.AssetId == assetId.Value);
        return await query
            .OrderByDescending(x => x.DetectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<GeofenceBreach> AddAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
    {
        dbContext.GeofenceBreaches.Add(breach);
        await dbContext.SaveChangesAsync(cancellationToken);
        return breach;
    }

    public async Task<GeofenceBreach?> AcknowledgeAsync(Guid id, DateTime acknowledgedAt, string? acknowledgedBy, CancellationToken ct = default)
    {
        var breach = await dbContext.GeofenceBreaches
            .Include(b => b.Geofence)
            .Include(b => b.Device)
            .Include(b => b.Asset)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (breach == null) return null;
        breach.AcknowledgedAtUtc = acknowledgedAt;
        breach.AcknowledgedBy = acknowledgedBy;
        await dbContext.SaveChangesAsync(ct);
        return breach;
    }

    public async Task<int> BulkAcknowledgeAsync(IEnumerable<Guid> ids, DateTime acknowledgedAt, string? acknowledgedBy, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        var breaches = await dbContext.GeofenceBreaches
            .Where(b => idList.Contains(b.Id) && b.AcknowledgedAtUtc == null)
            .ToListAsync(ct);
        foreach (var breach in breaches)
        {
            breach.AcknowledgedAtUtc = acknowledgedAt;
            breach.AcknowledgedBy = acknowledgedBy;
        }
        await dbContext.SaveChangesAsync(ct);
        return breaches.Count;
    }

    public async Task<int> GetUnacknowledgedCountAsync(CancellationToken ct = default)
        => await dbContext.GeofenceBreaches.CountAsync(b => b.AcknowledgedAtUtc == null, ct);

    public async Task<DeviceGeofenceState?> GetStateAsync(Guid deviceId, Guid geofenceId, CancellationToken ct = default)
        => await dbContext.DeviceGeofenceStates
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.GeofenceId == geofenceId, ct);

    public async Task UpsertStateAsync(DeviceGeofenceState state, CancellationToken ct = default)
    {
        var existing = await dbContext.DeviceGeofenceStates
            .FirstOrDefaultAsync(s => s.DeviceId == state.DeviceId && s.GeofenceId == state.GeofenceId, ct);
        if (existing is null)
        {
            dbContext.DeviceGeofenceStates.Add(state);
        }
        else if (state.LastObservationAt >= existing.LastObservationAt)
        {
            existing.IsInside = state.IsInside;
            existing.LastObservationAt = state.LastObservationAt;
            existing.UpdatedAt = state.UpdatedAt;
        }
        await dbContext.SaveChangesAsync(ct);
    }
}
