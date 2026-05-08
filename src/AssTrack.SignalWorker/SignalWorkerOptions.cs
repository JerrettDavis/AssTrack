namespace AssTrack.SignalWorker;

public sealed class SignalWorkerOptions
{
    public const string SectionName = "SignalWorker";

    public Uri? SignalBaseUrl { get; set; }
    public string Account { get; set; } = string.Empty;
    public int ReceiveTimeoutSeconds { get; set; } = 1;
}
