using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class DeviceRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Devices
            .Include(x => x.Asset)
            .Include(x => x.IntegrationFeed)
            .OrderBy(x => x.Identifier)
            .ToListAsync(cancellationToken);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .Include(x => x.Asset)
            .Include(x => x.IntegrationFeed)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Device?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .Include(x => x.Asset)
            .Include(x => x.IntegrationFeed)
            .FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);

    public Task<Device?> GetByIntegrationExternalIdAsync(Guid integrationFeedId, string externalId, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .Include(x => x.Asset)
            .Include(x => x.IntegrationFeed)
            .FirstOrDefaultAsync(x => x.IntegrationFeedId == integrationFeedId && x.ExternalId == externalId, cancellationToken);

    public async Task<Device> AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        dbContext.Devices.Add(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(device.Id, cancellationToken) ?? device;
    }

    public async Task<Device?> UpdateAsync(Guid id, string identifier, string? label, string? protocol, Guid? assetId, string? provider, string? externalId, string? tags, Guid? integrationFeedId, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return null;

        device.Identifier = identifier;
        device.Label = label;
        device.Protocol = string.IsNullOrWhiteSpace(protocol) ? "https" : protocol.Trim().ToLowerInvariant();
        device.AssetId = assetId;
        device.Provider = string.IsNullOrWhiteSpace(provider) ? "manual" : provider.Trim().ToLowerInvariant();
        device.ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        device.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim();
        device.IntegrationFeedId = integrationFeedId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Device?> EnrichAsync(Guid id, string? label, Guid? assetId, string? tags, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return null;

        if (!string.IsNullOrWhiteSpace(label))
        {
            device.Label = label.Trim();
        }

        if (assetId.HasValue)
        {
            device.AssetId = assetId;
        }

        if (!string.IsNullOrWhiteSpace(tags))
        {
            device.Tags = tags.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Device?> UpsertProviderProfileAsync(
        Guid id,
        string? providerLabel,
        string? providerLongName,
        string? providerShortName,
        string? providerHardwareModel,
        string? providerRole,
        string? providerProfileJson,
        DateTime? observedAt,
        string? tags,
        CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return null;

        device.ProviderLabel = FirstNonBlank(providerLabel, device.ProviderLabel);
        device.ProviderLongName = FirstNonBlank(providerLongName, device.ProviderLongName);
        device.ProviderShortName = FirstNonBlank(providerShortName, device.ProviderShortName);
        device.ProviderHardwareModel = FirstNonBlank(providerHardwareModel, device.ProviderHardwareModel);
        device.ProviderRole = FirstNonBlank(providerRole, device.ProviderRole);
        device.ProviderProfileJson = FirstNonBlank(providerProfileJson, device.ProviderProfileJson);
        device.ProviderProfileUpdatedAt = observedAt ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(tags))
        {
            device.Tags = tags.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return false;

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

