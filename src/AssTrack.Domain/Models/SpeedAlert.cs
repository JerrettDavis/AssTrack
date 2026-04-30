namespace AssTrack.Domain.Models;

public class SpeedAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ObservationId { get; set; }
    public Observation Observation { get; set; } = null!;
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public double ObservedSpeedKmh { get; set; }
    public double ThresholdKmh { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
}
