namespace AssTrack.Domain.Contracts;

public sealed record AssetDto(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<DeviceDto> Devices,
    double? SpeedThresholdKmh,
    bool IsSeeded);

public sealed record CreateAssetRequest(string Name, string? Description, string? Category, double? SpeedThresholdKmh = null);

public sealed record UpdateAssetRequest(string Name, string? Description, string? Category, double? SpeedThresholdKmh = null);
