using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Repositories;

public class CustodyRepository(AssTrackDbContext dbContext)
{
    public async Task<IReadOnlyList<CustodyEvent>> GetEventsAsync(Guid? assetId = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        var query = dbContext.CustodyEvents.Include(x => x.Asset).AsQueryable();
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId.Value);

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustodyEvent?> AddEventAsync(
        Guid assetId,
        string eventType,
        string? toCustodianName,
        string? toCustodianContact,
        string? custodyStatus,
        string? location,
        string? notes,
        DateTime occurredAt,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.Id == assetId, cancellationToken);
        if (asset is null) return null;

        var now = DateTime.UtcNow;
        var previousCustodian = asset.CustodianName;
        var status = ResolveStatus(eventType, custodyStatus);
        var nextCustodianName = eventType == CustodyEventTypes.CheckIn ? null : toCustodianName;
        var nextCustodianContact = eventType == CustodyEventTypes.CheckIn ? null : toCustodianContact;

        var custodyEvent = new CustodyEvent
        {
            AssetId = asset.Id,
            EventType = eventType,
            FromCustodianName = previousCustodian,
            ToCustodianName = nextCustodianName,
            ToCustodianContact = nextCustodianContact,
            Location = location,
            Notes = notes,
            OccurredAt = occurredAt,
            CreatedAt = now
        };

        asset.CustodyStatus = status;
        asset.CustodianName = nextCustodianName;
        asset.CustodianContact = nextCustodianContact;
        asset.CustodySince = status == AssetCustodyStatus.Available ? null : occurredAt;
        asset.UpdatedAt = now;

        dbContext.CustodyEvents.Add(custodyEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.CustodyEvents
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == custodyEvent.Id, cancellationToken);
    }

    private static string ResolveStatus(string eventType, string? requestedStatus)
    {
        if (!string.IsNullOrWhiteSpace(requestedStatus)) return requestedStatus;
        return eventType switch
        {
            CustodyEventTypes.CheckIn => AssetCustodyStatus.Available,
            CustodyEventTypes.Transfer => AssetCustodyStatus.CheckedOut,
            CustodyEventTypes.StatusChange => AssetCustodyStatus.InTransit,
            _ => AssetCustodyStatus.CheckedOut
        };
    }
}
