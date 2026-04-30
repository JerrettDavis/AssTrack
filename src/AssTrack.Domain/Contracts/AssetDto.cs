namespace AssTrack.Domain.Contracts;

public sealed record AssetDto(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<DeviceDto> Devices);

public sealed record CreateAssetRequest(string Name, string? Description, string? Category);

public sealed record UpdateAssetRequest(string Name, string? Description, string? Category);
