namespace AssTrack.Domain.Contracts;

public sealed record DeviceSummaryDto(
    Guid Id,
    string Identifier,
    string? Label,
    Guid? AssetId,
    string? AssetName,
    double? SpeedThresholdKmh,
    DateTime? LastSeenAt,
    double? LastLatitude,
    double? LastLongitude,
    double? LatestSpeedKmh,
    double? LatestHeadingDegrees,
    int UnacknowledgedSpeedAlerts,
    int UnacknowledgedGeofenceBreaches);
