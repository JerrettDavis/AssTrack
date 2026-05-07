using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/system/status", async (
            IWebHostEnvironment env,
            IConfiguration configuration,
            IOptions<SimulationOptions> simulationOptions,
            IOptions<WebhookOptions> webhookOptions,
            AssTrackDbContext db,
            CancellationToken ct) =>
        {
            var connStr = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("AssTrack")
                ?? "";
            var dbProvider = connStr.Contains("Data Source", StringComparison.OrdinalIgnoreCase) ? "SQLite" : "Unknown";

            var dto = new SystemStatusDto(
                Environment: env.EnvironmentName,
                SimulationEnabled: simulationOptions.Value.Enabled,
                WebhookConfigured: !string.IsNullOrEmpty(webhookOptions.Value.Url),
                ApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:ApiKey"]),
                IngestApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:IngestApiKey"]),
                SwaggerEnabled: configuration.GetValue<bool>("Swagger:Enabled"),
                RateLimitPermitLimit: configuration.GetValue<int>("RateLimiting:IngestPermitLimit", 60),
                RateLimitWindowSeconds: configuration.GetValue<int>("RateLimiting:IngestWindowSeconds", 60),
                DatabaseProvider: dbProvider,
                HasData: await db.Assets.AnyAsync(ct)
            );

            return Results.Ok(dto);
        })
        .WithName("GetSystemStatus")
        .WithSummary("Get sanitized system configuration status.")
        .RequireAuthorization("Operator");

        group.MapPost("/system/seed", async (
            SeedRequest request,
            ISeedService seedService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await seedService.SeedAsync(request.Reset, ct);
                return Results.Ok(result);
            }
            catch (SeedingDisabledException)
            {
                return Results.Forbid();
            }
        })
        .WithName("SeedDemoData")
        .WithSummary("Seed demo data. Use reset=true to wipe and re-seed seeded records only.")
        .RequireAuthorization("Operator");

        group.MapPost("/system/maintenance/clean-null-island", async (
            bool? dryRun,
            ObservationRepository observationRepository,
            CancellationToken ct) =>
        {
            var result = await observationRepository.DeleteNullIslandNoiseAsync(dryRun ?? true, ct);
            return Results.Ok(new ObservationCleanupResultDto(
                result.MatchingObservations,
                result.DeletedObservations,
                result.AffectedDevices,
                result.ResetGeofenceStates,
                dryRun ?? true));
        })
        .WithName("CleanNullIslandObservationNoise")
        .WithSummary("Remove historical observations near 0,0 and reset geofence state for affected devices.")
        .RequireAuthorization("Operator");

        group.MapPost("/system/maintenance/clean-auto-created-provider-assets", async (
            bool? dryRun,
            AssetRepository assetRepository,
            CancellationToken ct) =>
        {
            var result = await assetRepository.DeleteAutoCreatedProviderAssetsAsync(dryRun ?? true, ct);
            return Results.Ok(new AutoCreatedAssetCleanupResultDto(
                result.MatchingAssets,
                result.DeletedAssets,
                result.DetachedDevices,
                dryRun ?? true));
        })
        .WithName("CleanAutoCreatedProviderAssets")
        .WithSummary("Detach devices from previously auto-created provider assets and remove those asset records.")
        .RequireAuthorization("Operator");

        return group;
    }
}

