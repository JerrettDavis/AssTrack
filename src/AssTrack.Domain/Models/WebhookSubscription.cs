namespace AssTrack.Domain.Models;

public class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string EventTypes { get; set; } = "*";
    public string TargetUrl { get; set; } = string.Empty;
    public string? SigningSecret { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
