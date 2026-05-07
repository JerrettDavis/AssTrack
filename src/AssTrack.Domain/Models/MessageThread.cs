namespace AssTrack.Domain.Models;

public class MessageThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public Guid? IntegrationFeedId { get; set; }
    public IntegrationFeed? IntegrationFeed { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string? ExternalPeerId { get; set; }
    public string? DisplayName { get; set; }
    public string? Subject { get; set; }
    public string Status { get; set; } = MessageThreadStatus.Open;
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public ICollection<MessageEntry> Messages { get; set; } = new List<MessageEntry>();
}

public static class MessageThreadStatus
{
    public const string Open = "open";
    public const string Archived = "archived";
}
