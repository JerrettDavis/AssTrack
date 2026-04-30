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
}

