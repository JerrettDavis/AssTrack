using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class GeofenceRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Geofence>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Geofences
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Geofence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Geofences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Geofence> AddAsync(Geofence geofence, CancellationToken cancellationToken = default)
    {
        dbContext.Geofences.Add(geofence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return geofence;
    }
}
