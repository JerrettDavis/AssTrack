namespace AssTrack.Domain.Models;

/// <summary>
/// Geofence definition. Circles use a center point and radius; polygons use PolygonJson vertices.
/// </summary>
public class Geofence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ShapeType { get; set; } = "circle";
    public double CenterLatitude { get; set; }
    public double CenterLongitude { get; set; }
    public double RadiusMeters { get; set; }
    public string? PolygonJson { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
