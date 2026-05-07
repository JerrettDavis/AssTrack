namespace AssTrack.Domain.Contracts;

public sealed record IntegrationProviderDto(
    string Id,
    string Name,
    string Category,
    string ConnectionMode,
    bool SupportsDirectApi,
    bool SupportsWebhookIngest,
    bool SupportsPolling,
    string Status,
    string Description,
    string SetupNotes,
    IReadOnlyList<string> RecommendedTags);

public sealed record IntegrationFeedDto(
    Guid Id,
    string Name,
    string Provider,
    string ProviderName,
    bool IsEnabled,
    bool AutoCreateDevices,
    string? DefaultTags,
    string? ConfigurationJson,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int DeviceCount);

public sealed record CreateIntegrationFeedRequest(
    string Name,
    string Provider,
    bool IsEnabled = true,
    bool AutoCreateDevices = true,
    string? DefaultTags = null,
    string? ConfigurationJson = null);

public sealed record UpdateIntegrationFeedRequest(
    string Name,
    bool IsEnabled,
    bool AutoCreateDevices,
    string? DefaultTags,
    string? ConfigurationJson);

public sealed record IntegrationFeedObservationRequest(
    string ExternalTrackerId,
    DateTime ObservedAt,
    double Latitude,
    double Longitude,
    double? Altitude = null,
    double? AccuracyMeters = null,
    double? SpeedKmh = null,
    double? HeadingDegrees = null,
    string? Label = null,
    Guid? AssetId = null,
    string? Tags = null,
    string? Metadata = null);

public sealed record IntegrationDeviceProfileRequest(
    string ExternalTrackerId,
    DateTime? ObservedAt = null,
    string? Label = null,
    string? LongName = null,
    string? ShortName = null,
    string? HardwareModel = null,
    string? Role = null,
    string? Tags = null,
    Guid? AssetId = null,
    string? Metadata = null);

public sealed record IntegrationIngestResultDto(
    Guid FeedId,
    Guid DeviceId,
    string DeviceIdentifier,
    bool DeviceCreated,
    ObservationDto Observation);

public sealed record IntegrationDeviceProfileResultDto(
    Guid FeedId,
    Guid DeviceId,
    string DeviceIdentifier,
    bool DeviceCreated,
    Guid? AssetId,
    bool AssetCreated,
    string? Label);

public sealed record BridgeIntegrationFeedConfigDto(
    Guid FeedId,
    string Name,
    string Provider,
    bool IsEnabled,
    bool AutoCreateDevices,
    string? DefaultTags,
    string? ConfigurationJson,
    DateTime UpdatedAt);
