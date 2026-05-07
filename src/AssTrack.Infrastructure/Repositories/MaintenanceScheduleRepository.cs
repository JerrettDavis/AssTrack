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
        existing.DiagnosticSensorType = schedule.DiagnosticSensorType;
        existing.DiagnosticTextContains = schedule.DiagnosticTextContains;
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

    public async Task<MaintenanceServiceRecord?> AddServiceRecordAsync(
        Guid scheduleId,
        DateTime completedAt,
        double? odometerKm,
        double? runtimeHours,
        string? performedBy,
        decimal? cost,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var schedule = await dbContext.MaintenanceSchedules
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == scheduleId, cancellationToken);
        if (schedule is null) return null;

        var now = DateTime.UtcNow;
        var record = new MaintenanceServiceRecord
        {
            MaintenanceScheduleId = schedule.Id,
            AssetId = schedule.AssetId,
            CompletedAt = completedAt,
            OdometerKm = odometerKm,
            RuntimeHours = runtimeHours,
            PerformedBy = performedBy,
            Cost = cost,
            Notes = notes,
            CreatedAt = now
        };

        schedule.LastServiceAt = completedAt;
        if (odometerKm.HasValue) schedule.LastOdometerKm = odometerKm;
        if (runtimeHours.HasValue) schedule.LastRuntimeHours = runtimeHours;
        schedule.UpdatedAt = now;

        dbContext.MaintenanceServiceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await QueryRecords()
            .FirstOrDefaultAsync(x => x.Id == record.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceServiceRecord>> GetServiceRecordsAsync(
        Guid? scheduleId = null,
        Guid? assetId = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = QueryRecords();
        if (scheduleId.HasValue) query = query.Where(x => x.MaintenanceScheduleId == scheduleId.Value);
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId.Value);

        return await query
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<MaintenanceSchedule> Query()
        => dbContext.MaintenanceSchedules.Include(x => x.Asset);

    private IQueryable<MaintenanceServiceRecord> QueryRecords()
        => dbContext.MaintenanceServiceRecords
            .Include(x => x.Asset)
            .Include(x => x.MaintenanceSchedule);
}
