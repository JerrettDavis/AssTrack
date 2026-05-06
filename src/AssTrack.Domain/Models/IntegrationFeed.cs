namespace AssTrack.Domain.Models;

public class IntegrationFeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool AutoCreateDevices { get; set; } = true;
    public string? DefaultTags { get; set; }
    public string? ConfigurationJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
