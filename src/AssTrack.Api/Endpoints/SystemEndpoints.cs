using AssTrack.Api.Auth;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
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
                AdminApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:AdminApiKey"]),
                IngestApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:IngestApiKey"]),
                AccessTier: AssTrackAccessTiers.Normalize(configuration["Auth:AccessTier"]),
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
        .RequireAuthorization(AssTrackPolicies.Operator);

        group.MapPost("/system/seed", async (
            SeedRequest request,
            ISeedService seedService,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            try
            {
                var result = await seedService.SeedAsync(request.Reset, ct);
                broadcaster.PublishDataChanged("system", "seeded", metadata: new { request.Reset });
                await audit.RecordAsync(
                    httpContext,
                    "system.seed",
                    "system",
                    "seed",
                    "Demo data",
                    request.Reset ? "Reset and seeded demo data." : "Seeded demo data.",
                    new { request.Reset, result.AssetsCreated, result.DevicesCreated, result.GeofencesCreated },
                    ct);
                return Results.Ok(result);
            }
            catch (SeedingDisabledException)
            {
                return Results.Forbid();
            }
        })
        .WithName("SeedDemoData")
        .WithSummary("Seed demo data. Use reset=true to wipe and re-seed seeded records only.")
        .RequireAuthorization(AssTrackPolicies.Operator);

        group.MapPost("/system/maintenance/clean-null-island", async (
            bool? dryRun,
            ObservationRepository observationRepository,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            var result = await observationRepository.DeleteNullIslandNoiseAsync(dryRun ?? true, ct);
            if (result.DeletedObservations > 0) broadcaster.PublishDataChanged("observation", "cleaned", metadata: new { result.DeletedObservations, dryRun = dryRun ?? true });
            await audit.RecordAsync(
                httpContext,
                "maintenance.clean_null_island",
                "system_maintenance",
                "clean-null-island",
                "Null island cleanup",
                (dryRun ?? true) ? "Dry-ran null island observation cleanup." : "Deleted null island observation noise.",
                new { result.MatchingObservations, result.DeletedObservations, result.AffectedDevices, result.ResetGeofenceStates, dryRun = dryRun ?? true },
                ct);
            return Results.Ok(new ObservationCleanupResultDto(
                result.MatchingObservations,
                result.DeletedObservations,
                result.AffectedDevices,
                result.ResetGeofenceStates,
                dryRun ?? true));
        })
        .WithName("CleanNullIslandObservationNoise")
        .WithSummary("Remove historical observations near 0,0 and reset geofence state for affected devices.")
        .RequireAuthorization(AssTrackPolicies.Admin);

        group.MapPost("/system/maintenance/clean-auto-created-provider-assets", async (
            bool? dryRun,
            AssetRepository assetRepository,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            var result = await assetRepository.DeleteAutoCreatedProviderAssetsAsync(dryRun ?? true, ct);
            if (result.DeletedAssets > 0 || result.DetachedDevices > 0) broadcaster.PublishDataChanged("asset", "cleaned", metadata: new { result.DeletedAssets, result.DetachedDevices, dryRun = dryRun ?? true });
            await audit.RecordAsync(
                httpContext,
                "maintenance.clean_auto_provider_assets",
                "system_maintenance",
                "clean-auto-created-provider-assets",
                "Provider asset cleanup",
                (dryRun ?? true) ? "Dry-ran auto-created provider asset cleanup." : "Cleaned auto-created provider assets.",
                new { result.MatchingAssets, result.DeletedAssets, result.DetachedDevices, dryRun = dryRun ?? true },
                ct);
            return Results.Ok(new AutoCreatedAssetCleanupResultDto(
                result.MatchingAssets,
                result.DeletedAssets,
                result.DetachedDevices,
                dryRun ?? true));
        })
        .WithName("CleanAutoCreatedProviderAssets")
        .WithSummary("Detach devices from previously auto-created provider assets and remove those asset records.")
        .RequireAuthorization(AssTrackPolicies.Admin);

        group.MapPost("/system/maintenance/apply-retention", async (
            int? auditDays,
            int? signalDays,
            int? webhookDays,
            bool? dryRun,
            AssTrackDbContext db,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            var auditRetentionDays = ClampRetentionDays(auditDays, 365);
            var signalRetentionDays = ClampRetentionDays(signalDays, 180);
            var webhookRetentionDays = ClampRetentionDays(webhookDays, 90);
            var isDryRun = dryRun ?? true;
            var now = DateTime.UtcNow;
            var auditCutoff = now.AddDays(-auditRetentionDays);
            var signalCutoff = now.AddDays(-signalRetentionDays);
            var webhookCutoff = now.AddDays(-webhookRetentionDays);

            var matchingAuditEvents = await db.AuditEvents.CountAsync(x => x.OccurredAt < auditCutoff, ct);
            var matchingResolvedSignals = await db.IntegrationEvents.CountAsync(x =>
                x.Status == IntegrationEventStatuses.Resolved &&
                (x.ResolvedAt ?? x.OccurredAt) < signalCutoff, ct);
            var matchingWebhookDeliveries = await db.WebhookDeliveryLogs.CountAsync(x => x.AttemptedAt < webhookCutoff, ct);

            var deletedAuditEvents = 0;
            var deletedResolvedSignals = 0;
            var deletedWebhookDeliveries = 0;

            if (!isDryRun)
            {
                deletedAuditEvents = await db.AuditEvents
                    .Where(x => x.OccurredAt < auditCutoff)
                    .ExecuteDeleteAsync(ct);
                deletedResolvedSignals = await db.IntegrationEvents
                    .Where(x => x.Status == IntegrationEventStatuses.Resolved && (x.ResolvedAt ?? x.OccurredAt) < signalCutoff)
                    .ExecuteDeleteAsync(ct);
                deletedWebhookDeliveries = await db.WebhookDeliveryLogs
                    .Where(x => x.AttemptedAt < webhookCutoff)
                    .ExecuteDeleteAsync(ct);
            }

            if (deletedAuditEvents > 0 || deletedResolvedSignals > 0 || deletedWebhookDeliveries > 0)
            {
                broadcaster.PublishDataChanged("system", "retention_applied", metadata: new { deletedAuditEvents, deletedResolvedSignals, deletedWebhookDeliveries });
            }

            await audit.RecordAsync(
                httpContext,
                "maintenance.apply_retention",
                "system_maintenance",
                "enterprise-retention",
                "Enterprise retention cleanup",
                isDryRun ? "Dry-ran enterprise retention cleanup." : "Applied enterprise retention cleanup.",
                new
                {
                    auditRetentionDays,
                    signalRetentionDays,
                    webhookRetentionDays,
                    matchingAuditEvents,
                    deletedAuditEvents,
                    matchingResolvedSignals,
                    deletedResolvedSignals,
                    matchingWebhookDeliveries,
                    deletedWebhookDeliveries,
                    dryRun = isDryRun
                },
                ct);

            return Results.Ok(new EnterpriseRetentionCleanupResultDto(
                matchingAuditEvents,
                deletedAuditEvents,
                matchingResolvedSignals,
                deletedResolvedSignals,
                matchingWebhookDeliveries,
                deletedWebhookDeliveries,
                auditRetentionDays,
                signalRetentionDays,
                webhookRetentionDays,
                isDryRun));
        })
        .WithName("ApplyEnterpriseRetention")
        .WithSummary("Apply retention cleanup for audit events, resolved integration signals, and webhook delivery logs.")
        .RequireAuthorization(AssTrackPolicies.Admin);

        group.MapDelete("/system/maintenance/e2e-data", async (
            IWebHostEnvironment env,
            AssTrackDbContext db,
            IAuditService audit,
            HttpContext httpContext,
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
            await audit.RecordAsync(
                httpContext,
                "maintenance.clean_e2e_data",
                "system_maintenance",
                "e2e-data",
                "E2E data cleanup",
                "Removed non-production E2E data.",
                new { totalDeleted, deletedAssets, deletedDevices, deletedObservations, deletedSensorReadings, deletedAlerts, deletedBreaches, deletedStates, deletedSchedules, deletedServiceRecords, deletedCustodyEvents },
                ct);
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
        .RequireAuthorization(AssTrackPolicies.Admin);

        return group;
    }

    private static int ClampRetentionDays(int? value, int fallback)
        => Math.Clamp(value ?? fallback, 1, 36500);
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
