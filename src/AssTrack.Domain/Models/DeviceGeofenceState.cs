namespace AssTrack.Domain.Models;

/// <summary>
/// Tracks the last-known inside/outside state for a device/geofence pair.
/// Used to implement enter/exit transition semantics and suppress duplicate breach events.
/// </summary>
public class DeviceGeofenceState
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid GeofenceId { get; set; }
    public Geofence Geofence { get; set; } = null!;
    public bool IsInside { get; set; }
    public DateTime LastObservationAt { get; set; } = DateTime.MinValue;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
