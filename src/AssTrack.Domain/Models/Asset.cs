namespace AssTrack.Domain.Models;

public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssetClass { get; set; } = AssetClasses.Property;
    public string? Category { get; set; }
    public string Criticality { get; set; } = AssetCriticality.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public double? SpeedThresholdKmh { get; set; }
    public bool IsSeeded { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<MaintenanceServiceRecord> MaintenanceServiceRecords { get; set; } = new List<MaintenanceServiceRecord>();
}

public static class AssetClasses
{
    public const string Person = "person";
    public const string Vehicle = "vehicle";
    public const string Property = "property";
    public const string Pet = "pet";
    public const string Equipment = "equipment";
    public const string Container = "container";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Person,
        Vehicle,
        Property,
        Pet,
        Equipment,
        Container,
        Other
    };
}

public static class AssetCriticality
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Critical = "critical";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Low,
        Normal,
        High,
        Critical
    };
}
