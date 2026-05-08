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
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            try
            {
                var result = await seedService.SeedAsync(request.Reset, ct);
                broadcaster.PublishDataChanged("system", "seeded", metadata: new { request.Reset });
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
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            var result = await observationRepository.DeleteNullIslandNoiseAsync(dryRun ?? true, ct);
            if (result.DeletedObservations > 0) broadcaster.PublishDataChanged("observation", "cleaned", metadata: new { result.DeletedObservations, dryRun = dryRun ?? true });
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
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            var result = await assetRepository.DeleteAutoCreatedProviderAssetsAsync(dryRun ?? true, ct);
            if (result.DeletedAssets > 0 || result.DetachedDevices > 0) broadcaster.PublishDataChanged("asset", "cleaned", metadata: new { result.DeletedAssets, result.DetachedDevices, dryRun = dryRun ?? true });
            return Results.Ok(new AutoCreatedAssetCleanupResultDto(
                result.MatchingAssets,
                result.DeletedAssets,
                result.DetachedDevices,
                dryRun ?? true));
        })
        .WithName("CleanAutoCreatedProviderAssets")
        .WithSummary("Detach devices from previously auto-created provider assets and remove those asset records.")
        .RequireAuthorization("Operator");

        group.MapDelete("/system/maintenance/e2e-data", async (
            IWebHostEnvironment env,
            AssTrackDbContext db,
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            if (env.IsProduction())
            {
                return Results.Forbid();
            }

            var assetIds = await db.Assets
                .Where(asset => EF.Functions.Like(asset.Name, "E2E %"))
                .Select(asset => asset.Id)
                .ToListAsync(ct);

            var deviceIds = await db.Devices
                .Where(device =>
                    EF.Functions.Like(device.Identifier, "E2E-%") ||
                    EF.Functions.Like(device.Label ?? string.Empty, "E2E %") ||
                    EF.Functions.Like(device.Tags ?? string.Empty, "%e2e%") ||
                    (device.AssetId != null && assetIds.Contains(device.AssetId.Value)))
                .Select(device => device.Id)
                .ToListAsync(ct);

            var deletedBreaches = await db.GeofenceBreaches
                .Where(breach =>
                    deviceIds.Contains(breach.DeviceId) ||
                    (breach.AssetId != null && assetIds.Contains(breach.AssetId.Value)) ||
                    deviceIds.Contains(breach.Observation.DeviceId))
                .ExecuteDeleteAsync(ct);
            var deletedAlerts = await db.SpeedAlerts
                .Where(alert =>
                    deviceIds.Contains(alert.DeviceId) ||
                    (alert.AssetId != null && assetIds.Contains(alert.AssetId.Value)) ||
                    deviceIds.Contains(alert.Observation.DeviceId))
                .ExecuteDeleteAsync(ct);
            var deletedStates = await db.DeviceGeofenceStates
                .Where(state => deviceIds.Contains(state.DeviceId))
                .ExecuteDeleteAsync(ct);
            var deletedObservations = await db.Observations
                .Where(observation => deviceIds.Contains(observation.DeviceId))
                .ExecuteDeleteAsync(ct);
            var deletedSensorReadings = await db.SensorReadings
                .Where(reading =>
                    (reading.AssetId != null && assetIds.Contains(reading.AssetId.Value)) ||
                    (reading.DeviceId != null && deviceIds.Contains(reading.DeviceId.Value)))
                .ExecuteDeleteAsync(ct);
            var deletedServiceRecords = await db.MaintenanceServiceRecords
                .Where(record => assetIds.Contains(record.AssetId))
                .ExecuteDeleteAsync(ct);
            var deletedSchedules = await db.MaintenanceSchedules
                .Where(schedule => assetIds.Contains(schedule.AssetId))
                .ExecuteDeleteAsync(ct);
            var deletedCustodyEvents = await db.CustodyEvents
                .Where(custodyEvent => assetIds.Contains(custodyEvent.AssetId))
                .ExecuteDeleteAsync(ct);
            var deletedDevices = await db.Devices
                .Where(device => deviceIds.Contains(device.Id))
                .ExecuteDeleteAsync(ct);
            var deletedAssets = await db.Assets
                .Where(asset => assetIds.Contains(asset.Id))
                .ExecuteDeleteAsync(ct);

            var totalDeleted = deletedAssets + deletedDevices + deletedObservations + deletedSensorReadings + deletedAlerts + deletedBreaches + deletedStates + deletedSchedules + deletedServiceRecords + deletedCustodyEvents;
            if (totalDeleted > 0) broadcaster.PublishDataChanged("system", "cleaned_e2e_data", metadata: new { totalDeleted });
            return Results.Ok(new E2EDataCleanupResultDto(
                deletedAssets,
                deletedDevices,
                deletedObservations,
                deletedSensorReadings,
                deletedAlerts,
                deletedBreaches,
                deletedStates,
                deletedSchedules,
                deletedServiceRecords,
                deletedCustodyEvents));
        })
        .WithName("CleanE2EData")
        .WithSummary("Remove non-production E2E test records from a shared database.")
        .RequireAuthorization("Operator");

        return group;
    }
}

internal sealed record E2EDataCleanupResultDto(
    int DeletedAssets,
    int DeletedDevices,
    int DeletedObservations,
    int DeletedSensorReadings,
    int DeletedSpeedAlerts,
    int DeletedGeofenceBreaches,
    int DeletedDeviceGeofenceStates,
    int DeletedMaintenanceSchedules,
    int DeletedMaintenanceServiceRecords,
    int DeletedCustodyEvents);
