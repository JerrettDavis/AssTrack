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

            var asset = new Asset
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                Category = request.Category,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(asset, cancellationToken);
            return Results.Created($"/api/assets/{asset.Id}", Map(asset));
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
        asset.Devices.Select(DeviceEndpoints.Map).ToArray());
}
