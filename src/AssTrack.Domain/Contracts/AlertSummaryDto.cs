namespace AssTrack.Domain.Contracts;

public sealed record AlertSummaryDto(
    int UnacknowledgedSpeedAlerts,
    int UnacknowledgedBreaches);

public record BulkAcknowledgeSpeedAlertsRequest(IEnumerable<Guid> Ids, string? AcknowledgedBy);
public record BulkAcknowledgeBreachesRequest(IEnumerable<Guid> Ids, string? AcknowledgedBy);
