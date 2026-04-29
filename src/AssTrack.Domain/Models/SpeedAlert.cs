namespace AssTrack.Domain.Models;

public class SpeedAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ObservationId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? AssetId { get; set; }
    public double ObservedSpeedKmh { get; set; }
    public double ThresholdKmh { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
}
