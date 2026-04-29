namespace AssTrack.Domain.Contracts;

public sealed record DeviceDto(
    Guid Id,
    string Identifier,
    string? Label,
    string Protocol,
    DateTime CreatedAt,
    Guid? AssetId,
    string? AssetName);

public sealed record CreateDeviceRequest(string Identifier, string? Label, string? Protocol, Guid? AssetId);
