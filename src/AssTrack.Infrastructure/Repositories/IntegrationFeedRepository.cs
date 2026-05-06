using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class IntegrationFeedRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<IntegrationFeed>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.IntegrationFeeds
            .Include(x => x.Devices)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<IntegrationFeed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.IntegrationFeeds
            .Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IntegrationFeed> AddAsync(IntegrationFeed feed, CancellationToken cancellationToken = default)
    {
        dbContext.IntegrationFeeds.Add(feed);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(feed.Id, cancellationToken) ?? feed;
    }

    public async Task<IntegrationFeed?> UpdateAsync(Guid id, string name, bool isEnabled, bool autoCreateDevices, string? defaultTags, string? configurationJson, CancellationToken cancellationToken = default)
    {
        var feed = await dbContext.IntegrationFeeds.FindAsync([id], cancellationToken);
        if (feed is null) return null;

        feed.Name = name;
        feed.IsEnabled = isEnabled;
        feed.AutoCreateDevices = autoCreateDevices;
        feed.DefaultTags = string.IsNullOrWhiteSpace(defaultTags) ? null : defaultTags.Trim();
        feed.ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? null : configurationJson.Trim();
        feed.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var feed = await dbContext.IntegrationFeeds.FindAsync([id], cancellationToken);
        if (feed is null) return false;

        dbContext.IntegrationFeeds.Remove(feed);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
