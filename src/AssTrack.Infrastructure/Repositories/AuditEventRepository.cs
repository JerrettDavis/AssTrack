using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class AuditEventRepository(AssTrackDbContext dbContext)
{
    public async Task<AuditEvent> AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return auditEvent;
    }

    public async Task<(IReadOnlyList<AuditEvent> Items, int TotalCount)> SearchAsync(
        string? action,
        string? entityType,
        string? actor,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildQuery(action, entityType, actor, from, to);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<List<AuditEvent>> ExportAsync(
        string? action,
        string? entityType,
        string? actor,
        DateTime? from,
        DateTime? to,
        int limit = 10000,
        CancellationToken cancellationToken = default)
        => BuildQuery(action, entityType, actor, from, to)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(limit, 1, 10000))
            .ToListAsync(cancellationToken);

    private IQueryable<AuditEvent> BuildQuery(
        string? action,
        string? entityType,
        string? actor,
        DateTime? from,
        DateTime? to)
    {
        var query = dbContext.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(x => x.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(x => x.EntityType == normalizedEntityType);
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            var normalizedActor = actor.Trim();
            query = query.Where(x => x.ActorName.Contains(normalizedActor));
        }

        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);

        return query;
    }
}
