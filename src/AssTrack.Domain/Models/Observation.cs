namespace AssTrack.Domain.Models;

/// <summary>
/// Canonical telemetry model representing a single positional observation for a device.
/// Includes capture time, ingest time, coordinates, optional motion and quality fields, and JSON metadata.
/// </summary>
public class Observation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public DateTime ObservedAt { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedKmh { get; set; }
    public double? HeadingDegrees { get; set; }
    public string? Metadata { get; set; }
}
