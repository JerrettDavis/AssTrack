namespace AssTrack.Domain.Models;

public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssetClass { get; set; } = AssetClasses.Property;
    public string? Category { get; set; }
    public string Criticality { get; set; } = AssetCriticality.Normal;
    public string CustodyStatus { get; set; } = AssetCustodyStatus.Available;
    public string? CustodianName { get; set; }
    public string? CustodianContact { get; set; }
    public DateTime? CustodySince { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public double? SpeedThresholdKmh { get; set; }
    public bool IsSeeded { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<MaintenanceServiceRecord> MaintenanceServiceRecords { get; set; } = new List<MaintenanceServiceRecord>();
    public ICollection<CustodyEvent> CustodyEvents { get; set; } = new List<CustodyEvent>();
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

public class CustodyEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string EventType { get; set; } = CustodyEventTypes.CheckOut;
    public string? FromCustodianName { get; set; }
    public string? ToCustodianName { get; set; }
    public string? ToCustodianContact { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AssetCustodyStatus
{
    public const string Available = "available";
    public const string CheckedOut = "checked_out";
    public const string InTransit = "in_transit";
    public const string Maintenance = "maintenance";
    public const string Missing = "missing";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Available,
        CheckedOut,
        InTransit,
        Maintenance,
        Missing
    };
}

public static class CustodyEventTypes
{
    public const string CheckOut = "check_out";
    public const string CheckIn = "check_in";
    public const string Transfer = "transfer";
    public const string StatusChange = "status_change";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CheckOut,
        CheckIn,
        Transfer,
        StatusChange
    };
}
