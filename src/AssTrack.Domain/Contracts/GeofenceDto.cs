namespace AssTrack.Domain.Contracts;

public sealed record GeofenceDto(
    Guid Id,
    string Name,
    string? Description,
    string ShapeType,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    IReadOnlyList<GeofencePointDto>? PolygonCoordinates,
    bool IsActive,
    DateTime CreatedAt,
    bool IsSeeded);

public sealed record GeofencePointDto(double Latitude, double Longitude);

public sealed record CreateGeofenceRequest(
    string Name,
    string? Description,
    string? ShapeType,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    IReadOnlyList<GeofencePointDto>? PolygonCoordinates,
    bool? IsActive);

public sealed record UpdateGeofenceRequest(
    string Name,
    string? Description,
    string? ShapeType,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusMeters,
    IReadOnlyList<GeofencePointDto>? PolygonCoordinates,
    bool? IsActive);
