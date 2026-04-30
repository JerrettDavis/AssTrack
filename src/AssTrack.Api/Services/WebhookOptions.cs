namespace AssTrack.Api.Services;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>
    /// Target URL for outbound webhook POSTs. Leave empty to disable webhook delivery.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Seconds to wait for a response before timing out. Default is 10.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
