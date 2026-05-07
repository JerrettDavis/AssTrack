using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class MaintenanceScheduleRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<MaintenanceSchedule>> GetAllAsync(Guid? assetId = null, CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId.Value);

        return await query
            .OrderBy(x => x.Asset!.Name)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<MaintenanceSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<MaintenanceSchedule> AddAsync(MaintenanceSchedule schedule, CancellationToken cancellationToken = default)
    {
        dbContext.MaintenanceSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(schedule.Id, cancellationToken) ?? schedule;
    }

    public async Task<MaintenanceSchedule?> UpdateAsync(MaintenanceSchedule schedule, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MaintenanceSchedules.FindAsync([schedule.Id], cancellationToken);
        if (existing is null) return null;

        existing.AssetId = schedule.AssetId;
        existing.Title = schedule.Title;
        existing.ServiceType = schedule.ServiceType;
        existing.IntervalDays = schedule.IntervalDays;
        existing.IntervalOdometerKm = schedule.IntervalOdometerKm;
        existing.IntervalRuntimeHours = schedule.IntervalRuntimeHours;
        existing.LastServiceAt = schedule.LastServiceAt;
        existing.LastOdometerKm = schedule.LastOdometerKm;
        existing.LastRuntimeHours = schedule.LastRuntimeHours;
        existing.Notes = schedule.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(schedule.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await dbContext.MaintenanceSchedules.FindAsync([id], cancellationToken);
        if (schedule is null) return false;

        dbContext.MaintenanceSchedules.Remove(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<MaintenanceSchedule> Query()
        => dbContext.MaintenanceSchedules.Include(x => x.Asset);
}
