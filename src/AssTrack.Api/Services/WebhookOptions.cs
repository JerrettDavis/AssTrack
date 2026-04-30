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

    /// <summary>
    /// Maximum number of background retries after the first attempt fails.
    /// 0 (default) disables retries entirely.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Base delay in milliseconds for exponential back-off. Default is 1000.
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay cap in milliseconds for exponential back-off. Default is 30000.
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Optional HMAC-SHA256 signing secret. When set, outgoing webhook requests are signed
    /// with an X-AssTrack-Signature-256 header. Leave empty to disable signing.
    /// </summary>
    public string? SigningSecret { get; set; }
}
