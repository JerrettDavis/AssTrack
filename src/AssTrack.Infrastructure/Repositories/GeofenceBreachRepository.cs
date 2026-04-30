using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class GeofenceBreachRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<GeofenceBreach>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default)
        => await dbContext.GeofenceBreaches
            .Include(x => x.Geofence)
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .OrderByDescending(x => x.DetectedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

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
        else
        {
            existing.IsInside = state.IsInside;
            existing.UpdatedAt = state.UpdatedAt;
        }
        await dbContext.SaveChangesAsync(ct);
    }
}
