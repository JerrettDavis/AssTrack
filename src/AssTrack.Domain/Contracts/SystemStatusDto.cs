namespace AssTrack.Domain.Contracts;

public record SystemStatusDto(
    string Environment,
    bool SimulationEnabled,
    bool WebhookConfigured,
    bool ApiKeyConfigured,
    bool AdminApiKeyConfigured,
    bool IngestApiKeyConfigured,
    string AccessTier,
    bool SwaggerEnabled,
    int RateLimitPermitLimit,
    int RateLimitWindowSeconds,
    string DatabaseProvider,
    bool HasData
);

public record ObservationCleanupResultDto(
    int MatchingObservations,
    int DeletedObservations,
    int AffectedDevices,
    int ResetGeofenceStates,
    bool DryRun
);

public record AutoCreatedAssetCleanupResultDto(
    int MatchingAssets,
    int DeletedAssets,
    int DetachedDevices,
    bool DryRun
);

public record EnterpriseRetentionCleanupResultDto(
    int MatchingAuditEvents,
    int DeletedAuditEvents,
    int MatchingResolvedIntegrationEvents,
    int DeletedResolvedIntegrationEvents,
    int MatchingWebhookDeliveries,
    int DeletedWebhookDeliveries,
    int AuditRetentionDays,
    int SignalRetentionDays,
    int WebhookRetentionDays,
    bool DryRun
);
