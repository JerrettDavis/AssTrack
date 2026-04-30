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
    string DatabaseProvider
);
