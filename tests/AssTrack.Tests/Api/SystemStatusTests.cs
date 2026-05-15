using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class SystemStatusTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SystemStatusTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSystemStatus_WithApiKey_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemStatus_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemStatus_SimulationEnabled_IsBool()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        ((object)result!.SimulationEnabled).Should().BeOfType<bool>();
    }

    [Fact]
    public async Task GetSystemStatus_DatabaseProvider_IsSQLite()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.DatabaseProvider.Should().Be("SQLite");
    }

    [Fact]
    public async Task GetSystemStatus_RateLimitPermitLimit_IsPositive()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.RateLimitPermitLimit.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSystemStatus_IngestApiKeyConfigured_IsTrue_WhenSet()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.IngestApiKeyConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task GetSystemStatus_InvalidAccessTier_ReturnsEnterpriseFallback()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AccessTier"] = "invalid-tier"
        });
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/system/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.AccessTier.Should().Be("enterprise");
    }

    [Fact]
    public async Task CleanNullIsland_DryRun_DoesNotDeleteObservations()
    {
        await _factory.ResetDatabaseAsync();
        await SeedGoodAndNoisyObservationsAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=true", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ObservationCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingObservations.Should().Be(1);
        result.DeletedObservations.Should().Be(0);
        result.AffectedDevices.Should().Be(1);
        result.ResetGeofenceStates.Should().Be(1);
        result.DryRun.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await dbContext.Observations.CountAsync()).Should().Be(2);
        (await dbContext.DeviceGeofenceStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CleanNullIsland_DeletesNoise_AndResetsAffectedGeofenceState()
    {
        await _factory.ResetDatabaseAsync();
        var deviceId = await SeedGoodAndNoisyObservationsAsync();

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-null-island?dryRun=false", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ObservationCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingObservations.Should().Be(1);
        result.DeletedObservations.Should().Be(1);
        result.AffectedDevices.Should().Be(1);
        result.ResetGeofenceStates.Should().Be(1);
        result.DryRun.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var remaining = await dbContext.Observations.SingleAsync();
        remaining.DeviceId.Should().Be(deviceId);
        remaining.Latitude.Should().Be(36.0595);
        (await dbContext.DeviceGeofenceStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanAutoCreatedProviderAssets_DetachesMeshtasticDevices_AndDeletesGeneratedAssets()
    {
        await _factory.ResetDatabaseAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var feed = new IntegrationFeed { Name = "JDH Meshtastic", Provider = "meshtastic" };
            var generatedAsset = new Asset { Name = "!12f4fb74", Category = "Mesh node" };
            var manualAsset = new Asset { Name = "JDH Dev Node 0001", Category = "Mesh node", Description = "User enrolled asset" };
            dbContext.IntegrationFeeds.Add(feed);
            dbContext.Assets.AddRange(generatedAsset, manualAsset);
            dbContext.Devices.AddRange(
                new Device
                {
                    Identifier = "meshtastic:!12f4fb74",
                    Provider = "meshtastic",
                    Protocol = "meshtastic",
                    ExternalId = "!12f4fb74",
                    IntegrationFeed = feed,
                    Asset = generatedAsset
                },
                new Device
                {
                    Identifier = "meshtastic:!f6701924",
                    Provider = "meshtastic",
                    Protocol = "meshtastic",
                    ExternalId = "!f6701924",
                    IntegrationFeed = feed,
                    Asset = manualAsset
                });
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/system/maintenance/clean-auto-created-provider-assets?dryRun=false", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AutoCreatedAssetCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingAssets.Should().Be(1);
        result.DeletedAssets.Should().Be(1);
        result.DetachedDevices.Should().Be(1);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await verifyDb.Assets.SingleAsync()).Name.Should().Be("JDH Dev Node 0001");
        var detached = await verifyDb.Devices.SingleAsync(device => device.Identifier == "meshtastic:!12f4fb74");
        detached.AssetId.Should().BeNull();
        var retained = await verifyDb.Devices.SingleAsync(device => device.Identifier == "meshtastic:!f6701924");
        retained.AssetId.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyRetention_DryRunCountsButDoesNotDeleteEnterpriseRecords()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        await SeedRetentionRecordsAsync(factory);
        using var adminClient = factory.CreateClientWithApiKey("test-admin-key");

        var response = await adminClient.PostAsync("/api/system/maintenance/apply-retention?auditDays=30&signalDays=30&webhookDays=30&dryRun=true", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EnterpriseRetentionCleanupResultDto>();
        result.Should().NotBeNull();
        result!.MatchingAuditEvents.Should().Be(1);
        result.MatchingResolvedIntegrationEvents.Should().Be(1);
        result.MatchingWebhookDeliveries.Should().Be(1);
        result.DeletedAuditEvents.Should().Be(0);
        result.DeletedResolvedIntegrationEvents.Should().Be(0);
        result.DeletedWebhookDeliveries.Should().Be(0);
        result.DryRun.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await db.AuditEvents.CountAsync(x => x.EntityId == "old-audit")).Should().Be(1);
        (await db.IntegrationEvents.CountAsync(x => x.ExternalEventId == "old-resolved")).Should().Be(1);
        (await db.WebhookDeliveryLogs.CountAsync(x => x.CorrelationId == "old-webhook")).Should().Be(1);
    }

    [Fact]
    public async Task ApplyRetention_DeletesOldAuditResolvedSignalsAndWebhookLogs()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        await SeedRetentionRecordsAsync(factory);
        using var adminClient = factory.CreateClientWithApiKey("test-admin-key");

        var response = await adminClient.PostAsync("/api/system/maintenance/apply-retention?auditDays=30&signalDays=30&webhookDays=30&dryRun=false", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EnterpriseRetentionCleanupResultDto>();
        result.Should().NotBeNull();
        result!.DeletedAuditEvents.Should().Be(1);
        result.DeletedResolvedIntegrationEvents.Should().Be(1);
        result.DeletedWebhookDeliveries.Should().Be(1);
        result.DryRun.Should().BeFalse();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        (await db.AuditEvents.AnyAsync(x => x.EntityId == "old-audit")).Should().BeFalse();
        (await db.AuditEvents.AnyAsync(x => x.EntityId == "current-audit")).Should().BeTrue();
        (await db.AuditEvents.AnyAsync(x => x.Action == "maintenance.apply_retention")).Should().BeTrue();
        (await db.IntegrationEvents.AnyAsync(x => x.ExternalEventId == "old-resolved")).Should().BeFalse();
        (await db.IntegrationEvents.AnyAsync(x => x.ExternalEventId == "old-open")).Should().BeTrue();
        (await db.IntegrationEvents.AnyAsync(x => x.ExternalEventId == "current-resolved")).Should().BeTrue();
        (await db.WebhookDeliveryLogs.AnyAsync(x => x.CorrelationId == "old-webhook")).Should().BeFalse();
        (await db.WebhookDeliveryLogs.AnyAsync(x => x.CorrelationId == "current-webhook")).Should().BeTrue();
    }

    private async Task<Guid> SeedGoodAndNoisyObservationsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var device = new Device { Identifier = $"cleanup-dev-{Guid.NewGuid():N}" };
        var geofence = new Geofence
        {
            Name = "Cleanup state",
            CenterLatitude = 36.0595,
            CenterLongitude = -95.8976,
            RadiusMeters = 500,
            IsActive = true
        };
        dbContext.Devices.Add(device);
        dbContext.Geofences.Add(geofence);
        await dbContext.SaveChangesAsync();

        dbContext.Observations.AddRange(
            new Observation
            {
                DeviceId = device.Id,
                ObservedAt = DateTime.UtcNow.AddMinutes(-10),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                Latitude = 36.0595,
                Longitude = -95.8976
            },
            new Observation
            {
                DeviceId = device.Id,
                ObservedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Latitude = 0,
                Longitude = 0
            });
        dbContext.DeviceGeofenceStates.Add(new DeviceGeofenceState
        {
            DeviceId = device.Id,
            GeofenceId = geofence.Id,
            IsInside = false,
            LastObservationAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return device.Id;
    }

    private static async Task SeedRetentionRecordsAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var now = DateTime.UtcNow;
        db.AuditEvents.AddRange(
            new AuditEvent
            {
                OccurredAt = now.AddDays(-60),
                ActorName = "admin",
                ActorRole = "admin",
                Action = "old.action",
                EntityType = "test",
                EntityId = "old-audit"
            },
            new AuditEvent
            {
                OccurredAt = now.AddDays(-2),
                ActorName = "admin",
                ActorRole = "admin",
                Action = "current.action",
                EntityType = "test",
                EntityId = "current-audit"
            });
        db.IntegrationEvents.AddRange(
            new IntegrationEvent
            {
                OccurredAt = now.AddDays(-70),
                Source = "retention-test",
                ExternalEventId = "old-resolved",
                EventType = "test.old_resolved",
                Severity = IntegrationEventSeverities.Info,
                Message = "Old resolved signal.",
                Status = IntegrationEventStatuses.Resolved,
                ResolvedAt = now.AddDays(-60)
            },
            new IntegrationEvent
            {
                OccurredAt = now.AddDays(-70),
                Source = "retention-test",
                ExternalEventId = "old-open",
                EventType = "test.old_open",
                Severity = IntegrationEventSeverities.Warning,
                Message = "Old open signal.",
                Status = IntegrationEventStatuses.Open
            },
            new IntegrationEvent
            {
                OccurredAt = now.AddDays(-5),
                Source = "retention-test",
                ExternalEventId = "current-resolved",
                EventType = "test.current_resolved",
                Severity = IntegrationEventSeverities.Info,
                Message = "Current resolved signal.",
                Status = IntegrationEventStatuses.Resolved,
                ResolvedAt = now.AddDays(-2)
            });
        db.WebhookDeliveryLogs.AddRange(
            new WebhookDeliveryLog
            {
                AttemptedAt = now.AddDays(-70),
                EventType = "enterprise_signal",
                TargetUrl = "https://hooks.example.com/old",
                Success = true,
                DurationMs = 12,
                AttemptNumber = 1,
                CorrelationId = "old-webhook"
            },
            new WebhookDeliveryLog
            {
                AttemptedAt = now.AddDays(-2),
                EventType = "enterprise_signal",
                TargetUrl = "https://hooks.example.com/current",
                Success = true,
                DurationMs = 12,
                AttemptNumber = 1,
                CorrelationId = "current-webhook"
            });
        await db.SaveChangesAsync();
    }
}
