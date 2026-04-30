using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class GeofenceBreachRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<GeofenceBreach>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default)
        => await dbContext.GeofenceBreaches
            .Include(x => x.Geofence)
            .OrderByDescending(x => x.DetectedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<GeofenceBreach> AddAsync(GeofenceBreach breach, CancellationToken cancellationToken = default)
    {
        dbContext.GeofenceBreaches.Add(breach);
        await dbContext.SaveChangesAsync(cancellationToken);
        return breach;
    }
}
