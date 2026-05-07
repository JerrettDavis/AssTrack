using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class MessageRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<MessageThread>> GetThreadsAsync(CancellationToken cancellationToken = default)
        => await ThreadQuery()
            .OrderByDescending(x => x.LastMessageAt ?? x.UpdatedAt)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<MessageThread?> GetThreadAsync(Guid id, CancellationToken cancellationToken = default)
        => ThreadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MessageEntry>> GetMessagesAsync(Guid threadId, CancellationToken cancellationToken = default)
        => await dbContext.MessageEntries
            .AsNoTracking()
            .Where(x => x.ThreadId == threadId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<MessageThread> CreateThreadAsync(MessageThread thread, CancellationToken cancellationToken = default)
    {
        dbContext.MessageThreads.Add(thread);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetThreadAsync(thread.Id, cancellationToken) ?? thread;
    }

    public async Task<MessageThread> GetOrCreateThreadAsync(
        string channel,
        string provider,
        Guid? integrationFeedId,
        Guid? deviceId,
        Guid? assetId,
        string externalPeerId,
        string? displayName,
        string? metadata,
        CancellationToken cancellationToken = default)
    {
        var normalizedExternalPeerId = externalPeerId.Trim();
        var normalizedProvider = provider.Trim();
        var thread = await dbContext.MessageThreads
            .FirstOrDefaultAsync(x =>
                x.Provider == normalizedProvider &&
                x.ExternalPeerId == normalizedExternalPeerId &&
                x.IntegrationFeedId == integrationFeedId,
                cancellationToken);

        if (thread is not null)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(thread.DisplayName))
            {
                thread.DisplayName = displayName.Trim();
            }

            if (deviceId.HasValue && thread.DeviceId is null)
            {
                thread.DeviceId = deviceId;
            }

            if (assetId.HasValue && thread.AssetId is null)
            {
                thread.AssetId = assetId;
            }

            thread.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return await GetThreadAsync(thread.Id, cancellationToken) ?? thread;
        }

        thread = new MessageThread
        {
            Channel = channel.Trim(),
            Provider = normalizedProvider,
            IntegrationFeedId = integrationFeedId,
            DeviceId = deviceId,
            AssetId = assetId,
            ExternalPeerId = normalizedExternalPeerId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedExternalPeerId : displayName.Trim(),
            Metadata = string.IsNullOrWhiteSpace(metadata) ? null : metadata.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await CreateThreadAsync(thread, cancellationToken);
    }

    public async Task<MessageEntry> AddMessageAsync(MessageEntry message, CancellationToken cancellationToken = default)
    {
        dbContext.MessageEntries.Add(message);
        var thread = await dbContext.MessageThreads.FindAsync([message.ThreadId], cancellationToken);
        if (thread is not null)
        {
            thread.LastMessageAt = message.CreatedAt;
            thread.UpdatedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<MessageEntry>> GetQueuedOutboundAsync(Guid integrationFeedId, int take, CancellationToken cancellationToken = default)
        => await dbContext.MessageEntries
            .AsNoTracking()
            .Include(x => x.Thread)
            .Where(x =>
                x.Direction == MessageDirection.Outbound &&
                x.Status == MessageStatus.Queued &&
                x.Thread != null &&
                x.Thread.IntegrationFeedId == integrationFeedId)
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<MessageEntry?> UpdateStatusAsync(
        Guid messageId,
        string status,
        string? providerMessageId,
        DateTime? sentAt,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var message = await dbContext.MessageEntries
            .Include(x => x.Thread)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null) return null;

        message.Status = status.Trim();
        message.ProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId) ? message.ProviderMessageId : providerMessageId.Trim();
        message.SentAt = sentAt?.ToUniversalTime() ?? message.SentAt;
        message.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        if (message.Thread is not null)
        {
            message.Thread.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    private IQueryable<MessageThread> ThreadQuery()
        => dbContext.MessageThreads
            .AsNoTracking()
            .Include(x => x.IntegrationFeed)
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .Include(x => x.Messages.OrderByDescending(message => message.CreatedAt).Take(1));
}
