using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class AssetRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Assets
            .Include(x => x.Devices)
            .Include(x => x.SensorReadings.OrderByDescending(reading => reading.ObservedAt).Take(8))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Assets
            .Include(x => x.Devices)
            .Include(x => x.SensorReadings.OrderByDescending(reading => reading.ObservedAt).Take(25))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Asset> AddAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<Asset?> UpdateAsync(Guid id, string name, string? description, string? assetClass, string? category, string? criticality, double? speedThresholdKmh, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FindAsync([id], cancellationToken);
        if (asset is null) return null;

        asset.Name = name;
        asset.Description = description;
        asset.AssetClass = assetClass ?? asset.AssetClass;
        asset.Category = category;
        asset.Criticality = criticality ?? asset.Criticality;
        asset.SpeedThresholdKmh = speedThresholdKmh;
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

    public async Task<AutoCreatedAssetCleanupResult> DeleteAutoCreatedProviderAssetsAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var assets = await dbContext.Assets
            .Include(asset => asset.Devices)
            .Where(asset =>
                !asset.IsSeeded &&
                asset.Category == "Mesh node" &&
                asset.Description == null &&
                asset.Devices.Count > 0 &&
                asset.Devices.All(device =>
                    device.Provider == "meshtastic" &&
                    device.IntegrationFeedId != null &&
                    device.ExternalId != null))
            .ToListAsync(cancellationToken);

        var detachedDevices = assets.Sum(asset => asset.Devices.Count);
        if (dryRun || assets.Count == 0)
        {
            return new AutoCreatedAssetCleanupResult(assets.Count, 0, detachedDevices);
        }

        foreach (var device in assets.SelectMany(asset => asset.Devices))
        {
            device.AssetId = null;
        }

        dbContext.Assets.RemoveRange(assets);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AutoCreatedAssetCleanupResult(assets.Count, assets.Count, detachedDevices);
    }
}

public sealed record AutoCreatedAssetCleanupResult(int MatchingAssets, int DeletedAssets, int DetachedDevices);

