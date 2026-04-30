namespace AssTrack.Domain.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identifier { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string Protocol { get; set; } = "https";
    public bool IsSeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<Observation> Observations { get; set; } = new List<Observation>();
}
