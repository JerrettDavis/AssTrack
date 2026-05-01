namespace AssTrack.Domain.Models;

/// <summary>
/// Append-only audit record of every outbound webhook delivery attempt.
/// Deliberately has no FK constraints — it is standalone and must never block ingest.
/// </summary>
public class WebhookDeliveryLog
{
    public int Id { get; set; }
    public DateTime AttemptedAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? HttpStatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>First 500 characters of the serialised request payload.</summary>
    public string? RequestPayloadSummary { get; set; }
    /// <summary>1-based attempt counter; 1 = first try, 2+ = retries.</summary>
    public int AttemptNumber { get; set; } = 1;
    /// <summary>Groups all retry attempts for the same trigger event.</summary>
    public string CorrelationId { get; set; } = string.Empty;
}
