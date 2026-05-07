namespace AssTrack.Domain.Models;

public class MaintenanceSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string ServiceType { get; set; } = MaintenanceServiceTypes.General;
    public int? IntervalDays { get; set; }
    public double? IntervalOdometerKm { get; set; }
    public double? IntervalRuntimeHours { get; set; }
    public string? DiagnosticSensorType { get; set; }
    public string? DiagnosticTextContains { get; set; }
    public DateTime? LastServiceAt { get; set; }
    public double? LastOdometerKm { get; set; }
    public double? LastRuntimeHours { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MaintenanceServiceRecord> ServiceRecords { get; set; } = new List<MaintenanceServiceRecord>();
}

public class MaintenanceServiceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenanceScheduleId { get; set; }
    public MaintenanceSchedule MaintenanceSchedule { get; set; } = null!;
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public double? OdometerKm { get; set; }
    public double? RuntimeHours { get; set; }
    public string? PerformedBy { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class MaintenanceServiceTypes
{
    public const string General = "general";
    public const string Oil = "oil";
    public const string Inspection = "inspection";
    public const string Tire = "tire";
    public const string Battery = "battery";
    public const string Calibration = "calibration";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        General,
        Oil,
        Inspection,
        Tire,
        Battery,
        Calibration
    };
}

public static class MaintenanceStatus
{
    public const string Current = "current";
    public const string Upcoming = "upcoming";
    public const string Due = "due";
    public const string Overdue = "overdue";
}
