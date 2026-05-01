using AssTrack.Domain.Models;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace AssTrack.Api.Services;

/// <summary>
/// Background service that drains the in-memory retry channel and re-attempts
/// failed webhook deliveries with exponential back-off.
/// Loss on application restart is acceptable — retries are best-effort.
/// </summary>
public sealed class WebhookRetryWorker : BackgroundService
{
    private readonly ChannelReader<WebhookRetryJob> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WebhookOptions> _options;
    private readonly ILogger<WebhookRetryWorker> _logger;

    public WebhookRetryWorker(
        ChannelReader<WebhookRetryJob> reader,
        IServiceScopeFactory scopeFactory,
        IOptions<WebhookOptions> options,
        ILogger<WebhookRetryWorker> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _reader.ReadAllAsync(stoppingToken))
        {
            // Exponential back-off: min(base * 2^(attempt-1), max)
            // attempt=2 → base*1, attempt=3 → base*2, etc.
            var exponent = job.AttemptNumber - 2; // 0-based for the first retry
            var delayMs = Math.Min(
                _options.Value.RetryBaseDelayMs * (1 << Math.Max(0, exponent)),
                _options.Value.RetryMaxDelayMs);

            _logger.LogWarning(
                "WebhookRetryWorker: waiting {DelayMs}ms before attempt {Attempt} (correlationId {CorrelationId})",
                delayMs, job.AttemptNumber, job.CorrelationId);

            try
            {
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IWebhookNotificationService>();
            await svc.ExecuteRetryAsync(job, stoppingToken);
        }
    }
}
