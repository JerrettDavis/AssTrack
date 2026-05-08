namespace AssTrack.MessageBridge.Worker;

public sealed class BridgeWorkerOptions
{
    public const string SectionName = "BridgeWorker";

    public Uri? BridgeBaseUrl { get; set; }
    public string FeedKey { get; set; } = string.Empty;
    public string? SharedSecret { get; set; }
    public string DefaultChannel { get; set; } = "direct";
    public int PollSeconds { get; set; } = 5;
    public int OutboundTake { get; set; } = 25;
}
