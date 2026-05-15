using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class WebhookSubscriptionRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.WebhookSubscriptions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> GetEnabledForEventAsync(string eventType, CancellationToken cancellationToken = default)
    {
        var subscriptions = await dbContext.WebhookSubscriptions
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return subscriptions
            .Where(x => MatchesEventType(x.EventTypes, eventType))
            .ToList();
    }

    public Task<int> CountEnabledAsync(CancellationToken cancellationToken = default)
        => dbContext.WebhookSubscriptions.CountAsync(x => x.IsEnabled, cancellationToken);

    public Task<WebhookSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<WebhookSubscription?> GetEnabledForTargetAsync(string targetUrl, string eventType, CancellationToken cancellationToken = default)
    {
        var subscriptions = await GetEnabledForEventAsync(eventType, cancellationToken);
        return subscriptions.FirstOrDefault(x => string.Equals(x.TargetUrl, targetUrl, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<WebhookSubscription> AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        dbContext.WebhookSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task<WebhookSubscription?> UpdateAsync(
        Guid id,
        string name,
        bool isEnabled,
        string eventTypes,
        string targetUrl,
        string? signingSecret,
        CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.WebhookSubscriptions.FindAsync([id], cancellationToken);
        if (subscription is null) return null;

        subscription.Name = name;
        subscription.IsEnabled = isEnabled;
        subscription.EventTypes = eventTypes;
        subscription.TargetUrl = targetUrl;
        subscription.SigningSecret = signingSecret;
        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.WebhookSubscriptions.FindAsync([id], cancellationToken);
        if (subscription is null) return false;

        dbContext.WebhookSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool MatchesEventType(string eventTypes, string eventType)
    {
        var values = eventTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return values.Length == 0 ||
            values.Any(value =>
                value == "*" ||
                string.Equals(value, eventType, StringComparison.OrdinalIgnoreCase));
    }
}
