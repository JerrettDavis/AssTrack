using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class DeviceRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Devices
            .Include(x => x.Asset)
            .OrderBy(x => x.Identifier)
            .ToListAsync(cancellationToken);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Device?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);

    public async Task<Device> AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        dbContext.Devices.Add(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(device.Id, cancellationToken) ?? device;
    }

    public async Task<Device?> UpdateAsync(Guid id, string identifier, string? label, string? protocol, Guid? assetId, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return null;

        device.Identifier = identifier;
        device.Label = label;
        device.Protocol = string.IsNullOrWhiteSpace(protocol) ? "https" : protocol.Trim().ToLowerInvariant();
        device.AssetId = assetId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices.FindAsync([id], cancellationToken);
        if (device is null) return false;

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

