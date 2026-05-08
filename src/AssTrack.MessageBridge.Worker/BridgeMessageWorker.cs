using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssTrack.MessageBridge.Worker;

public sealed class BridgeMessageWorker(BridgeMessagePump pump, ILogger<BridgeMessageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Message bridge worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await pump.ExecuteOnceAsync(stoppingToken);
            await Task.Delay(pump.PollInterval, stoppingToken);
        }
    }
}
