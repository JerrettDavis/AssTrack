namespace AssTrack.Domain.Contracts;

public sealed record GeofenceDto(
    Guid Id,
    string Name,
    string? Description,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateGeofenceRequest(
    string Name,
    string? Description,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    bool? IsActive);

public sealed record UpdateGeofenceRequest(
    string Name,
    string? Description,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    bool? IsActive);
