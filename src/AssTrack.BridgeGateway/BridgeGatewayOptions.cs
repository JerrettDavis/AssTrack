namespace AssTrack.BridgeGateway;

public sealed class BridgeGatewayOptions
{
    public const string SectionName = "BridgeGateway";

    public Uri? AssTrackBaseUrl { get; set; }
    public string? IngestApiKey { get; set; }
    public string? OperatorApiKey { get; set; }
    public bool DryRun { get; set; }
    public int BridgeConfigRefreshSeconds { get; set; } = 30;
    public Dictionary<string, BridgeFeedOptions> Feeds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BridgeFeedOptions
{
    public bool Enabled { get; set; } = true;
    public Guid FeedId { get; set; }
    public string Provider { get; set; } = "generic-webhook";
    public string? SharedSecret { get; set; }
    public Guid? AssetId { get; set; }
    public string? DefaultTags { get; set; }
    public string? LabelPrefix { get; set; }
    public string? ConfigurationJson { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
