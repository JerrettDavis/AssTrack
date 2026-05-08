using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using AssTrack.Api;
using AssTrack.Api.Services;

namespace AssTrack.Api.Endpoints;

public static class CustodyEndpoints
{
    public static RouteGroupBuilder MapCustodyEndpoints(this RouteGroupBuilder group)
    {
        var custody = group.MapGroup("/custody");

        custody.MapGet("/events", async (
            Guid? assetId,
            int? limit,
            CustodyRepository repository,
            CancellationToken cancellationToken) =>
        {
            var events = await repository.GetEventsAsync(assetId, limit ?? 200, cancellationToken);
            return Results.Ok(events.Select(Map));
        }).RequireAuthorization("Operator");

        custody.MapPost("/events", async (
            CreateCustodyEventRequest request,
            CustodyRepository repository,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            var eventType = NormalizeEventType(request.EventType);
            var custodyStatus = NormalizeCustodyStatus(request.CustodyStatus);
            var validation = Validate(request, eventType, custodyStatus);
            if (validation.Count > 0) return Results.ValidationProblem(validation);

            var custodyEvent = await repository.AddEventAsync(
                request.AssetId,
                eventType!,
                Clean(request.ToCustodianName),
                Clean(request.ToCustodianContact),
                custodyStatus,
                Clean(request.Location),
                Clean(request.Notes),
                request.OccurredAt ?? DateTime.UtcNow,
                cancellationToken);

            return custodyEvent is null
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["assetId"] = ["Asset is required."] })
                : CreatedCustodyEvent(custodyEvent, broadcaster);
        }).RequireAuthorization("Operator");

        return group;
    }

    internal static CustodyEventDto Map(CustodyEvent custodyEvent) => new(
        custodyEvent.Id,
        custodyEvent.AssetId,
        custodyEvent.Asset?.Name,
        custodyEvent.EventType,
        custodyEvent.FromCustodianName,
        custodyEvent.ToCustodianName,
        custodyEvent.ToCustodianContact,
        custodyEvent.Location,
        custodyEvent.Notes,
        ApiDateTime.Utc(custodyEvent.OccurredAt),
        ApiDateTime.Utc(custodyEvent.CreatedAt));

    private static IResult CreatedCustodyEvent(CustodyEvent custodyEvent, ILiveEventBroadcaster broadcaster)
    {
        broadcaster.PublishDataChanged("custody_event", "created", custodyEvent.Id, new { custodyEvent.AssetId });
        return Results.Created($"/api/custody/events/{custodyEvent.Id}", Map(custodyEvent));
    }

    private static Dictionary<string, string[]> Validate(CreateCustodyEventRequest request, string? eventType, string? custodyStatus)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.AssetId == Guid.Empty) errors["assetId"] = ["Asset is required."];
        if (eventType is null) errors["eventType"] = ["Custody event type is not supported."];
        if (!string.IsNullOrWhiteSpace(request.CustodyStatus) && custodyStatus is null) errors["custodyStatus"] = ["Custody status is not supported."];
        if (request.OccurredAt.HasValue && request.OccurredAt.Value > DateTime.UtcNow.AddMinutes(5)) errors["occurredAt"] = ["Occurred time cannot be in the future."];
        if (eventType is CustodyEventTypes.CheckOut or CustodyEventTypes.Transfer && string.IsNullOrWhiteSpace(request.ToCustodianName)) errors["toCustodianName"] = ["Custodian is required."];
        if (eventType == CustodyEventTypes.CheckIn && (!string.IsNullOrWhiteSpace(request.ToCustodianName) || !string.IsNullOrWhiteSpace(request.ToCustodianContact))) errors["toCustodianName"] = ["Check-in events cannot assign a custodian."];
        return errors;
    }

    private static string? NormalizeEventType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return CustodyEventTypes.All.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeCustodyStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return AssetCustodyStatus.All.Contains(normalized) ? normalized : null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
