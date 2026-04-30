using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Endpoints;

public static class SpeedAlertEndpoints
{
    public static RouteGroupBuilder MapSpeedAlertEndpoints(this RouteGroupBuilder group)
    {
        var alerts = group.MapGroup("/speed-alerts");

        alerts.MapGet(string.Empty, async (SpeedAlertRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetRecentAsync(cancellationToken: cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        return group;
    }

    private static SpeedAlertDto Map(AssTrack.Domain.Models.SpeedAlert alert) => new(
        alert.Id,
        alert.ObservationId,
        alert.DeviceId,
        alert.AssetId,
        alert.ObservedSpeedKmh,
        alert.ThresholdKmh,
        alert.TriggeredAt,
        alert.Device?.Identifier,
        alert.Asset?.Name);
}

