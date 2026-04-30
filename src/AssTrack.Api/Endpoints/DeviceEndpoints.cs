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
        });

        devices.MapGet("/{id:guid}", async (Guid id, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var device = await repository.GetByIdAsync(id, cancellationToken);
            return device is null ? Results.NotFound() : Results.Ok(Map(device));
        });

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
        });

        devices.MapPut("/{id:guid}", async (Guid id, UpdateDeviceRequest request, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Identifier))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["identifier"] = ["Identifier is required."] });
            }

            var updated = await repository.UpdateAsync(id, request.Identifier.Trim(), request.Label, request.Protocol, request.AssetId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        });

        devices.MapDelete("/{id:guid}", async (Guid id, DeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    internal static DeviceDto Map(Device device) => new(
        device.Id,
        device.Identifier,
        device.Label,
        device.Protocol,
        device.CreatedAt,
        device.AssetId,
        device.Asset?.Name);
}

