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
}
