namespace AssTrack.Domain.Models;

/// <summary>
/// Recorded when an observation is detected inside an active geofence.
/// </summary>
public class GeofenceBreach
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ObservationId { get; set; }
    public Observation Observation { get; set; } = null!;
    public Guid GeofenceId { get; set; }
    public Geofence Geofence { get; set; } = null!;
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
    public GeofenceBreachEventType EventType { get; set; } = GeofenceBreachEventType.Enter;
}
