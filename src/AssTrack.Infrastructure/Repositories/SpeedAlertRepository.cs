using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class SpeedAlertRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<SpeedAlert>> GetRecentAsync(int count = 100, CancellationToken cancellationToken = default)
        => await dbContext.SpeedAlerts
            .Include(x => x.Device)
            .Include(x => x.Asset)
            .OrderByDescending(x => x.TriggeredAt)
            .Take(count)
            .ToListAsync(cancellationToken);

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

    public async Task<bool> HasRecentAlertAsync(Guid deviceId, TimeSpan cooldown, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - cooldown;
        return await dbContext.SpeedAlerts.AnyAsync(a => a.DeviceId == deviceId && a.TriggeredAt >= cutoff, ct);
    }
}

