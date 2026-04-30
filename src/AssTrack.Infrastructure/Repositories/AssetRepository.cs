using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class AssetRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Assets
            .Include(x => x.Devices)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Assets
            .Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Asset> AddAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<Asset?> UpdateAsync(Guid id, string name, string? description, string? category, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FindAsync([id], cancellationToken);
        if (asset is null) return null;

        asset.Name = name;
        asset.Description = description;
        asset.Category = category;
        asset.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FindAsync([id], cancellationToken);
        if (asset is null) return false;

        dbContext.Assets.Remove(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

