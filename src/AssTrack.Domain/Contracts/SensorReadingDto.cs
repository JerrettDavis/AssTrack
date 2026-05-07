namespace AssTrack.Domain.Contracts;

public sealed record SensorReadingDto(
    Guid Id,
    Guid? AssetId,
    string? AssetName,
    Guid? DeviceId,
    string? DeviceIdentifier,
    Guid? IntegrationFeedId,
    string SensorType,
    string? Name,
    double? NumericValue,
    string? TextValue,
    string? Unit,
    DateTime ObservedAt,
    DateTime ReceivedAt,
    string? Metadata);

public sealed record CreateSensorReadingRequest(
    Guid? AssetId,
    Guid? DeviceId,
    string? DeviceIdentifier,
    Guid? IntegrationFeedId,
    string SensorType,
    string? Name,
    double? NumericValue,
    string? TextValue,
    string? Unit,
    DateTime? ObservedAt,
    string? Metadata);
