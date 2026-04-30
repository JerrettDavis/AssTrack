using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        var devices = group.MapGroup("/devices");

        devices.MapGet(string.Empty, async (DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        }).RequireAuthorization("Operator");

        devices.MapGet("/{id:guid}", async (Guid id, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var device = await repository.GetByIdAsync(id, cancellationToken);
            return device is null ? Results.NotFound() : Results.Ok(Map(device));
        }).RequireAuthorization("Operator");

        devices.MapGet("/{id:guid}/summary", async (Guid id, DeviceRepository deviceRepository, ObservationRepository observationRepository, SpeedAlertRepository speedAlertRepository, GeofenceBreachRepository geofenceBreachRepository, CancellationToken cancellationToken) =>
        {
            var device = await deviceRepository.GetByIdAsync(id, cancellationToken);
            if (device is null)
                return Results.NotFound();

            var latestObservation = await observationRepository.GetLatestForDeviceAsync(id, cancellationToken);
            var unacknowledgedSpeedAlerts = await speedAlertRepository.GetRecentAsync(limit: int.MaxValue, unacknowledgedOnly: true, since: null, deviceId: id, assetId: null, cancellationToken: cancellationToken);
            var unacknowledgedBreaches = await geofenceBreachRepository.GetRecentAsync(limit: int.MaxValue, unacknowledgedOnly: true, since: null, deviceId: id, assetId: null, cancellationToken: cancellationToken);

            var summary = new DeviceSummaryDto(
                device.Id,
                device.Identifier,
                device.Label,
                device.AssetId,
                device.Asset?.Name,
                device.Asset?.SpeedThresholdKmh,
                latestObservation?.ObservedAt,
                latestObservation?.Latitude,
                latestObservation?.Longitude,
                latestObservation?.SpeedKmh,
                latestObservation?.HeadingDegrees,
                unacknowledgedSpeedAlerts.Count,
                unacknowledgedBreaches.Count
            );

            return Results.Ok(summary);
        }).RequireAuthorization("Operator");

        devices.MapPost(string.Empty, async (CreateDeviceRequest request, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Identifier))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["identifier"] = ["Identifier is required."] });
            }

            var existing = await repository.GetByIdentifierAsync(request.Identifier.Trim(), cancellationToken);
            if (existing is not null)
            {
                return Results.Problem(title: "Conflict", detail: "A device with this identifier already exists.", statusCode: StatusCodes.Status409Conflict);
            }

            var device = new Device
            {
                Identifier = request.Identifier.Trim(),
                Label = request.Label,
                Protocol = string.IsNullOrWhiteSpace(request.Protocol) ? "https" : request.Protocol.Trim().ToLowerInvariant(),
                AssetId = request.AssetId,
                CreatedAt = DateTime.UtcNow
            };

            var created = await repository.AddAsync(device, cancellationToken);
            return Results.Created($"/api/devices/{created.Id}", Map(created));
        }).RequireAuthorization("Operator");

        devices.MapPut("/{id:guid}", async (Guid id, UpdateDeviceRequest request, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Identifier))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["identifier"] = ["Identifier is required."] });
            }

            var updated = await repository.UpdateAsync(id, request.Identifier.Trim(), request.Label, request.Protocol, request.AssetId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        }).RequireAuthorization("Operator");

        devices.MapDelete("/{id:guid}", async (Guid id, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Operator");

        return group;
    }

    internal static DeviceDto Map(Device device) => new(
        device.Id,
        device.Identifier,
        device.Label,
        device.Protocol,
        device.CreatedAt,
        device.AssetId,
        device.Asset?.Name,
        device.IsSeeded);
}

