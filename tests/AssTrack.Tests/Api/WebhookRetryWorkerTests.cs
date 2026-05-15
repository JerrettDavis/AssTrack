using System.Threading.Channels;
using AssTrack.Api.Services;
using AssTrack.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssTrack.Tests.Api;

public class WebhookRetryWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_DrainsChannel_AndExecutesRetry()
    {
        var retryService = new CapturingWebhookNotificationService();
        var services = new ServiceCollection()
            .AddSingleton<IWebhookNotificationService>(retryService)
            .BuildServiceProvider();

        var channel = Channel.CreateUnbounded<WebhookRetryJob>();
        var worker = new WebhookRetryWorker(
            channel.Reader,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WebhookOptions { RetryBaseDelayMs = 1, RetryMaxDelayMs = 1 }),
            NullLogger<WebhookRetryWorker>.Instance);

        var job = new WebhookRetryJob(
            Guid.NewGuid(),
            """{"eventType":"speed_alert"}""",
            "speed_alert",
            2,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow);

        await worker.StartAsync(CancellationToken.None);
        await channel.Writer.WriteAsync(job);

        var completed = await retryService.WaitForRetryAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        await services.DisposeAsync();

        completed.Should().BeTrue();
        retryService.Job.Should().Be(job);
    }

    private sealed class CapturingWebhookNotificationService : IWebhookNotificationService
    {
        private readonly TaskCompletionSource<WebhookRetryJob> _retry =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WebhookRetryJob? Job { get; private set; }

        public Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyIntegrationEventAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ExecuteRetryAsync(WebhookRetryJob job, CancellationToken cancellationToken = default)
        {
            Job = job;
            _retry.TrySetResult(job);
            return Task.CompletedTask;
        }

        public async Task<bool> WaitForRetryAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_retry.Task, Task.Delay(timeout));
            return completed == _retry.Task;
        }
    }
}
