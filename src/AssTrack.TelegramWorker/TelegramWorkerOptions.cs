namespace AssTrack.TelegramWorker;

public sealed class TelegramWorkerOptions
{
    public const string SectionName = "TelegramWorker";

    public Uri ApiBaseUrl { get; set; } = new("https://api.telegram.org");
    public string BotToken { get; set; } = string.Empty;
    public int ReceiveLimit { get; set; } = 50;
    public string? OffsetFile { get; set; }
}
