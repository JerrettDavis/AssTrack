using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using AssTrack.Api;
using AssTrack.Api.Services;

namespace AssTrack.Api.Endpoints;

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        var assets = group.MapGroup("/assets");

        assets.MapGet("/classes", () => Results.Ok(AssetClassCatalog.All))
            .RequireAuthorization("Operator");

        assets.MapGet(string.Empty, async (AssetRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        }).RequireAuthorization("Operator");

        assets.MapGet("/{id:guid}", async (Guid id, AssetRepository repository, CancellationToken cancellationToken) =>
        {
            var asset = await repository.GetByIdAsync(id, cancellationToken);
            return asset is null ? Results.NotFound() : Results.Ok(Map(asset));
        }).RequireAuthorization("Operator");

        assets.MapPost(string.Empty, async (CreateAssetRequest request, AssetRepository repository, ILiveEventBroadcaster broadcaster, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

            if (request.SpeedThresholdKmh.HasValue && (double.IsNaN(request.SpeedThresholdKmh.Value) || double.IsInfinity(request.SpeedThresholdKmh.Value) || request.SpeedThresholdKmh.Value <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["speedThresholdKmh"] = ["Speed threshold must be a valid positive number."] });
            }

            var assetClass = NormalizeAssetClass(request.AssetClass);
            var criticality = NormalizeCriticality(request.Criticality);
            var custodyStatus = NormalizeCustodyStatus(request.CustodyStatus);
            if (assetClass is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["assetClass"] = ["Asset class is not supported."] });
            }

            if (criticality is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["criticality"] = ["Criticality is not supported."] });
            }

            if (!string.IsNullOrWhiteSpace(request.CustodyStatus) && custodyStatus is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["custodyStatus"] = ["Custody status is not supported."] });
            }

            var now = DateTime.UtcNow;
            var effectiveCustodyStatus = custodyStatus ?? AssetCustodyStatus.Available;
            var custodianName = Clean(request.CustodianName);
            var custodianContact = Clean(request.CustodianContact);

            var asset = new Asset
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                AssetClass = assetClass,
                Category = request.Category,
                Criticality = criticality,
                CustodyStatus = effectiveCustodyStatus,
                CustodianName = custodianName,
                CustodianContact = custodianContact,
                CustodySince = custodianName is not null || custodianContact is not null || effectiveCustodyStatus != AssetCustodyStatus.Available ? now : null,
                SpeedThresholdKmh = request.SpeedThresholdKmh,
                CreatedAt = now,
                UpdatedAt = now
            };

            await repository.AddAsync(asset, cancellationToken);
            broadcaster.PublishDataChanged("asset", "created", asset.Id);
            return Results.Created($"/api/assets/{asset.Id}", Map(asset));
        }).RequireAuthorization("Operator");

        assets.MapPut("/{id:guid}", async (Guid id, UpdateAssetRequest request, AssetRepository repository, ILiveEventBroadcaster broadcaster, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

            if (request.SpeedThresholdKmh.HasValue && (double.IsNaN(request.SpeedThresholdKmh.Value) || double.IsInfinity(request.SpeedThresholdKmh.Value) || request.SpeedThresholdKmh.Value <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["speedThresholdKmh"] = ["Speed threshold must be a valid positive number."] });
            }

            var assetClass = NormalizeAssetClass(request.AssetClass);
            var criticality = NormalizeCriticality(request.Criticality);
            var custodyStatus = NormalizeCustodyStatus(request.CustodyStatus);
            if (assetClass is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["assetClass"] = ["Asset class is not supported."] });
            }

            if (criticality is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["criticality"] = ["Criticality is not supported."] });
            }

            if (!string.IsNullOrWhiteSpace(request.CustodyStatus) && custodyStatus is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["custodyStatus"] = ["Custody status is not supported."] });
            }

            var updated = await repository.UpdateAsync(id, request.Name.Trim(), request.Description, assetClass, request.Category, criticality, custodyStatus, Clean(request.CustodianName), Clean(request.CustodianContact), request.SpeedThresholdKmh, cancellationToken);
            if (updated is not null) broadcaster.PublishDataChanged("asset", "updated", updated.Id);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        }).RequireAuthorization("Operator");

        assets.MapDelete("/{id:guid}", async (Guid id, AssetRepository repository, ILiveEventBroadcaster broadcaster, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            if (deleted) broadcaster.PublishDataChanged("asset", "deleted", id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Operator");

        return group;
    }

    internal static AssetDto Map(Asset asset) => new(
        asset.Id,
        asset.Name,
        asset.Description,
        asset.AssetClass,
        asset.Category,
        asset.Criticality,
        asset.CustodyStatus,
        asset.CustodianName,
        asset.CustodianContact,
        ApiDateTime.Utc(asset.CustodySince),
        ApiDateTime.Utc(asset.CreatedAt),
        ApiDateTime.Utc(asset.UpdatedAt),
        asset.Devices.Select(DeviceEndpoints.Map).ToArray(),
        asset.SensorReadings
            .OrderByDescending(x => x.ObservedAt)
            .GroupBy(x => x.SensorType)
            .Select(group => SensorEndpoints.Map(group.First()))
            .ToArray(),
        asset.SpeedThresholdKmh,
        asset.IsSeeded);

    private static string? NormalizeAssetClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AssetClasses.Property;
        var normalized = value.Trim().ToLowerInvariant();
        return AssetClasses.All.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeCriticality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AssetCriticality.Normal;
        var normalized = value.Trim().ToLowerInvariant();
        return AssetCriticality.All.Contains(normalized) ? normalized : null;
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

public sealed record AssetClassDto(string Id, string Name, string Description, IReadOnlyList<string> RecommendedSensors);

public static class AssetClassCatalog
{
    public static readonly IReadOnlyList<AssetClassDto> All =
    [
        new(AssetClasses.Person, "Person", "People, teams, responders, or family members with privacy-sensitive tracking.", ["battery", "sos", "motion"]),
        new(AssetClasses.Vehicle, "Vehicle", "Cars, trucks, vans, ATVs, boats, and other powered mobile assets.", ["battery", "fuel", "odometer", "engine", "tire_pressure"]),
        new(AssetClasses.Property, "Property", "Structures, places, depots, camps, or fixed assets.", ["temperature", "humidity", "motion", "door"]),
        new(AssetClasses.Pet, "Pet", "Pets and working animals where lightweight wearable trackers are common.", ["battery", "temperature", "motion"]),
        new(AssetClasses.Equipment, "Equipment", "Tools, generators, machinery, and field equipment.", ["battery", "impact", "runtime", "temperature"]),
        new(AssetClasses.Container, "Container", "Trailers, cases, cargo, totes, and containers.", ["door", "temperature", "humidity", "impact"]),
        new(AssetClasses.Other, "Other", "Tracked assets that do not fit a standard class yet.", ["battery"])
    ];
}
