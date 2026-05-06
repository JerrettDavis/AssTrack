namespace AssTrack.BridgeGateway;

public sealed record ProviderObservation(
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
