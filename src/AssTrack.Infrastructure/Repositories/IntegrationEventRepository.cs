using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class IntegrationEventRepository(AssTrackDbContext dbContext)
{
    public async Task<IntegrationEvent> AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        dbContext.IntegrationEvents.Add(integrationEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return integrationEvent;
    }

    public async Task<(IReadOnlyList<IntegrationEvent> Items, int TotalCount)> SearchAsync(
        string? source,
        string? externalEventId,
        string? eventType,
        string? severity,
        string? status,
        string? subjectType,
        string? subjectId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildQuery(source, externalEventId, eventType, severity, status, subjectType, subjectId, from, to);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<List<IntegrationEvent>> ExportAsync(
        string? source,
        string? externalEventId,
        string? eventType,
        string? severity,
        string? status,
        string? subjectType,
        string? subjectId,
        DateTime? from,
        DateTime? to,
        int limit = 10000,
        CancellationToken cancellationToken = default)
        => BuildQuery(source, externalEventId, eventType, severity, status, subjectType, subjectId, from, to)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(limit, 1, 10000))
            .ToListAsync(cancellationToken);

    private IQueryable<IntegrationEvent> BuildQuery(
        string? source,
        string? externalEventId,
        string? eventType,
        string? severity,
        string? status,
        string? subjectType,
        string? subjectId,
        DateTime? from,
        DateTime? to)
    {
        var query = dbContext.IntegrationEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(source))
        {
            var normalizedSource = source.Trim();
            query = query.Where(x => x.Source == normalizedSource);
        }

        if (!string.IsNullOrWhiteSpace(externalEventId))
        {
            var normalizedExternalEventId = externalEventId.Trim();
            query = query.Where(x => x.ExternalEventId == normalizedExternalEventId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalizedEventType = eventType.Trim();
            query = query.Where(x => x.EventType == normalizedEventType);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalizedSeverity = severity.Trim();
            query = query.Where(x => x.Severity == normalizedSeverity);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(subjectType))
        {
            var normalizedSubjectType = subjectType.Trim();
            query = query.Where(x => x.SubjectType == normalizedSubjectType);
        }

        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            var normalizedSubjectId = subjectId.Trim();
            query = query.Where(x => x.SubjectId == normalizedSubjectId);
        }

        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);

        return query;
    }

    public Task<IntegrationEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.IntegrationEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<IntegrationEvent?> GetBySourceExternalEventIdAsync(
        string source,
        string externalEventId,
        CancellationToken cancellationToken = default)
        => dbContext.IntegrationEvents.AsNoTracking().FirstOrDefaultAsync(
            x => x.Source == source && x.ExternalEventId == externalEventId,
            cancellationToken);

    public async Task<IntegrationEvent?> AcknowledgeAsync(
        Guid id,
        string actor,
        DateTime acknowledgedAt,
        CancellationToken cancellationToken = default)
    {
        var integrationEvent = await dbContext.IntegrationEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (integrationEvent is null) return null;
        if (integrationEvent.Status == IntegrationEventStatuses.Resolved) return integrationEvent;

        integrationEvent.Status = IntegrationEventStatuses.Acknowledged;
        integrationEvent.AcknowledgedAt = acknowledgedAt;
        integrationEvent.AcknowledgedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);
        return integrationEvent;
    }

    public async Task<IntegrationEvent?> ResolveAsync(
        Guid id,
        string actor,
        DateTime resolvedAt,
        string? resolutionNote,
        CancellationToken cancellationToken = default)
    {
        var integrationEvent = await dbContext.IntegrationEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (integrationEvent is null) return null;

        integrationEvent.Status = IntegrationEventStatuses.Resolved;
        integrationEvent.ResolvedAt = resolvedAt;
        integrationEvent.ResolvedBy = actor;
        integrationEvent.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();
        integrationEvent.AcknowledgedAt ??= resolvedAt;
        integrationEvent.AcknowledgedBy ??= actor;
        await dbContext.SaveChangesAsync(cancellationToken);
        return integrationEvent;
    }
}
