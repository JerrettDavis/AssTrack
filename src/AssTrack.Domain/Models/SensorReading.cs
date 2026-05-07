namespace AssTrack.Domain.Models;

public class SensorReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? IntegrationFeedId { get; set; }
    public IntegrationFeed? IntegrationFeed { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public string? Name { get; set; }
    public double? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public string? Unit { get; set; }
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }
}

public static class SensorTypes
{
    public const string Battery = "battery";
    public const string Temperature = "temperature";
    public const string Humidity = "humidity";
    public const string Fuel = "fuel";
    public const string Odometer = "odometer";
    public const string Engine = "engine";
    public const string TirePressure = "tire_pressure";
    public const string Impact = "impact";
    public const string Door = "door";
    public const string Motion = "motion";
    public const string Light = "light";
    public const string Custom = "custom";
}
