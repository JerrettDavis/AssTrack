namespace AssTrack.BridgeGateway;

public sealed record ProviderDeviceProfile(
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
