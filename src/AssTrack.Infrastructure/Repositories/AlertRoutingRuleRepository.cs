using AssTrack.Domain.Models;
using Microsoft.EntityFrameworkCore;
using AssTrack.Infrastructure.Data;

namespace AssTrack.Infrastructure.Repositories;

public class AlertRoutingRuleRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<AlertRoutingRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Query()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertRoutingRule>> GetEnabledForEventAsync(string eventType, Guid? assetId, CancellationToken cancellationToken = default)
        => await Query()
            .Where(x => x.IsEnabled && (x.EventType == AlertRouteEventTypes.All || x.EventType == eventType))
            .Where(x => !x.AssetId.HasValue || (assetId.HasValue && x.AssetId == assetId.Value))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<AlertRoutingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<AlertRoutingRule> AddAsync(AlertRoutingRule rule, CancellationToken cancellationToken = default)
    {
        dbContext.AlertRoutingRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(rule.Id, cancellationToken) ?? rule;
    }

    public async Task<AlertRoutingRule?> UpdateAsync(
        Guid id,
        string name,
        bool isEnabled,
        string eventType,
        string channel,
        string provider,
        Guid? assetId,
        Guid? integrationFeedId,
        string? externalPeerId,
        string? displayName,
        string? recipient,
        string? messageTemplate,
        CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.AlertRoutingRules.FindAsync([id], cancellationToken);
        if (rule is null) return null;

        rule.Name = name;
        rule.IsEnabled = isEnabled;
        rule.EventType = eventType;
        rule.Channel = channel;
        rule.Provider = provider;
        rule.AssetId = assetId;
        rule.IntegrationFeedId = integrationFeedId;
        rule.ExternalPeerId = externalPeerId;
        rule.DisplayName = displayName;
        rule.Recipient = recipient;
        rule.MessageTemplate = messageTemplate;
        rule.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken) ?? rule;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.AlertRoutingRules.FindAsync([id], cancellationToken);
        if (rule is null) return false;

        dbContext.AlertRoutingRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<AlertRoutingRule> Query()
        => dbContext.AlertRoutingRules
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.IntegrationFeed);
}
