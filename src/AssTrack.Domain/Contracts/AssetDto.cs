namespace AssTrack.Domain.Contracts;

public sealed record AssetDto(
    Guid Id,
    string Name,
    string? Description,
    string AssetClass,
    string? Category,
    string Criticality,
    string CustodyStatus,
    string? CustodianName,
    string? CustodianContact,
    DateTime? CustodySince,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<DeviceDto> Devices,
    IReadOnlyCollection<SensorReadingDto> LatestSensorReadings,
    double? SpeedThresholdKmh,
    bool IsSeeded);

public sealed record CreateAssetRequest(
    string Name,
    string? Description,
    string? Category,
    double? SpeedThresholdKmh = null,
    string? AssetClass = null,
    string? Criticality = null,
    string? CustodyStatus = null,
    string? CustodianName = null,
    string? CustodianContact = null);

public sealed record UpdateAssetRequest(
    string Name,
    string? Description,
    string? Category,
    double? SpeedThresholdKmh = null,
    string? AssetClass = null,
    string? Criticality = null,
    string? CustodyStatus = null,
    string? CustodianName = null,
    string? CustodianContact = null);
