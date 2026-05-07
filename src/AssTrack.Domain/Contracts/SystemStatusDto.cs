namespace AssTrack.Domain.Contracts;

public record SystemStatusDto(
    string Environment,
    bool SimulationEnabled,
    bool WebhookConfigured,
    bool ApiKeyConfigured,
    bool IngestApiKeyConfigured,
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
