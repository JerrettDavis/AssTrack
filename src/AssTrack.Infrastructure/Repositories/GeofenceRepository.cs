using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class GeofenceRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Geofence>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await dbContext.Geofences
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

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

    public async Task<Geofence?> UpdateAsync(Guid id, string name, string? description, string shapeType, double centerLatitude, double centerLongitude, double radiusMeters, string? polygonJson, bool? isActive, CancellationToken cancellationToken = default)
    {
        var geofence = await dbContext.Geofences.FindAsync([id], cancellationToken);
        if (geofence is null) return null;

        geofence.Name = name;
        geofence.Description = description;
        geofence.ShapeType = shapeType;
        geofence.CenterLatitude = centerLatitude;
        geofence.CenterLongitude = centerLongitude;
        geofence.RadiusMeters = radiusMeters;
        geofence.PolygonJson = polygonJson;
        geofence.IsActive = isActive ?? geofence.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return geofence;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var geofence = await dbContext.Geofences.FindAsync([id], cancellationToken);
        if (geofence is null) return false;

        dbContext.Geofences.Remove(geofence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

