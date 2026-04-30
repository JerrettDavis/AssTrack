namespace AssTrack.Domain.Models;

/// <summary>
/// Circular geofence definition with a center point and radius in meters for simple inclusion checks.
/// </summary>
public class Geofence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double CenterLatitude { get; set; }
    public double CenterLongitude { get; set; }
    public double RadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
