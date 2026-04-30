using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class SpeedAlertRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<SpeedAlert>> GetRecentAsync(
        int limit = 100,
        bool? unacknowledgedOnly = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SpeedAlerts
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .AsQueryable();
        if (unacknowledgedOnly == true)
            query = query.Where(x => x.AcknowledgedAtUtc == null);
        if (since.HasValue)
            query = query.Where(x => x.TriggeredAt >= since.Value);
        return await query
            .OrderByDescending(x => x.TriggeredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<SpeedAlert?> AcknowledgeAsync(Guid id, DateTime acknowledgedAt, string? acknowledgedBy, CancellationToken ct = default)
    {
        var alert = await dbContext.SpeedAlerts
            .Include(a => a.Device)
            .Include(a => a.Asset)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert == null) return null;
        alert.AcknowledgedAtUtc = acknowledgedAt;
        alert.AcknowledgedBy = acknowledgedBy;
        await dbContext.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<int> BulkAcknowledgeAsync(IEnumerable<Guid> ids, DateTime acknowledgedAt, string? acknowledgedBy, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        var alerts = await dbContext.SpeedAlerts
            .Where(a => idList.Contains(a.Id) && a.AcknowledgedAtUtc == null)
            .ToListAsync(ct);
        foreach (var alert in alerts)
        {
            alert.AcknowledgedAtUtc = acknowledgedAt;
            alert.AcknowledgedBy = acknowledgedBy;
        }
        await dbContext.SaveChangesAsync(ct);
        return alerts.Count;
    }

    public async Task<int> GetUnacknowledgedCountAsync(CancellationToken ct = default)
        => await dbContext.SpeedAlerts.CountAsync(a => a.AcknowledgedAtUtc == null, ct);

    public async Task<bool> HasRecentAlertAsync(Guid deviceId, TimeSpan cooldown, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - cooldown;
        return await dbContext.SpeedAlerts.AnyAsync(a => a.DeviceId == deviceId && a.TriggeredAt >= cutoff, ct);
    }
}

