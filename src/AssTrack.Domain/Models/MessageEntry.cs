namespace AssTrack.Domain.Models;

public class MessageEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public MessageThread? Thread { get; set; }
    public string Direction { get; set; } = MessageDirection.Inbound;
    public string Status { get; set; } = MessageStatus.Received;
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public string? Metadata { get; set; }
}

public static class MessageDirection
{
    public const string Inbound = "inbound";
    public const string Outbound = "outbound";
    public const string System = "system";
}

public static class MessageStatus
{
    public const string Received = "received";
    public const string Queued = "queued";
    public const string Sent = "sent";
    public const string Delivered = "delivered";
    public const string Failed = "failed";
}
