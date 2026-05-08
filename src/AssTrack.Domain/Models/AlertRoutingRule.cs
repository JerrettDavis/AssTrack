namespace AssTrack.Domain.Models;

public class AlertRoutingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string EventType { get; set; } = AlertRouteEventTypes.All;
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public Guid? IntegrationFeedId { get; set; }
    public IntegrationFeed? IntegrationFeed { get; set; }
    public string? ExternalPeerId { get; set; }
    public string? DisplayName { get; set; }
    public string? Recipient { get; set; }
    public string? MessageTemplate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class AlertRouteEventTypes
{
    public const string All = "all";
    public const string SpeedAlert = "speed_alert";
    public const string GeofenceBreach = "geofence_breach";
}
