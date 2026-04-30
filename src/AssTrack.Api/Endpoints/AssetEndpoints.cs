using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Endpoints;

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        var assets = group.MapGroup("/assets");

        assets.MapGet(string.Empty, async (AssetRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        assets.MapGet("/{id:guid}", async (Guid id, AssetRepository repository, CancellationToken cancellationToken) =>
        {
            var asset = await repository.GetByIdAsync(id, cancellationToken);
            return asset is null ? Results.NotFound() : Results.Ok(Map(asset));
        });

        assets.MapPost(string.Empty, async (CreateAssetRequest request, AssetRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

            if (request.SpeedThresholdKmh.HasValue && (double.IsNaN(request.SpeedThresholdKmh.Value) || double.IsInfinity(request.SpeedThresholdKmh.Value) || request.SpeedThresholdKmh.Value <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["speedThresholdKmh"] = ["Speed threshold must be a valid positive number."] });
            }

            var asset = new Asset
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                Category = request.Category,
                SpeedThresholdKmh = request.SpeedThresholdKmh,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(asset, cancellationToken);
            return Results.Created($"/api/assets/{asset.Id}", Map(asset));
        });

        assets.MapPut("/{id:guid}", async (Guid id, UpdateAssetRequest request, AssetRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

            if (request.SpeedThresholdKmh.HasValue && (double.IsNaN(request.SpeedThresholdKmh.Value) || double.IsInfinity(request.SpeedThresholdKmh.Value) || request.SpeedThresholdKmh.Value <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["speedThresholdKmh"] = ["Speed threshold must be a valid positive number."] });
            }

            var updated = await repository.UpdateAsync(id, request.Name.Trim(), request.Description, request.Category, request.SpeedThresholdKmh, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        });

        assets.MapDelete("/{id:guid}", async (Guid id, AssetRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    internal static AssetDto Map(Asset asset) => new(
        asset.Id,
        asset.Name,
        asset.Description,
        asset.Category,
        asset.CreatedAt,
        asset.UpdatedAt,
        asset.Devices.Select(DeviceEndpoints.Map).ToArray(),
        asset.SpeedThresholdKmh);
}

