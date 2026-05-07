namespace AssTrack.Domain.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identifier { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string Protocol { get; set; } = "https";
    public string Provider { get; set; } = "manual";
    public string? ExternalId { get; set; }
    public string? Tags { get; set; }
    public string? ProviderLabel { get; set; }
    public string? ProviderLongName { get; set; }
    public string? ProviderShortName { get; set; }
    public string? ProviderHardwareModel { get; set; }
    public string? ProviderRole { get; set; }
    public string? ProviderProfileJson { get; set; }
    public DateTime? ProviderProfileUpdatedAt { get; set; }
    public bool IsSeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid? IntegrationFeedId { get; set; }
    public IntegrationFeed? IntegrationFeed { get; set; }
    public ICollection<Observation> Observations { get; set; } = new List<Observation>();
}
