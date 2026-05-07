namespace AssTrack.Domain.Contracts;

public sealed record DeviceDto(
    Guid Id,
    string Identifier,
    string? Label,
    string Protocol,
    DateTime CreatedAt,
    Guid? AssetId,
    string? AssetName,
    bool IsSeeded,
    string Provider,
    string? ExternalId,
    string? Tags,
    Guid? IntegrationFeedId,
    string? IntegrationFeedName,
    string? ProviderLabel,
    string? ProviderLongName,
    string? ProviderShortName,
    string? ProviderHardwareModel,
    string? ProviderRole,
    string? ProviderProfileJson,
    DateTime? ProviderProfileUpdatedAt);

public sealed record CreateDeviceRequest(string Identifier, string? Label, string? Protocol, Guid? AssetId, string? Provider = null, string? ExternalId = null, string? Tags = null, Guid? IntegrationFeedId = null);

public sealed record UpdateDeviceRequest(string Identifier, string? Label, string? Protocol, Guid? AssetId, string? Provider = null, string? ExternalId = null, string? Tags = null, Guid? IntegrationFeedId = null);
