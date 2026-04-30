using AssTrack.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AssTrack.Tests.Api;

public class LiveEventBroadcasterTests
{
    private static LiveEventBroadcaster CreateBroadcaster() =>
        new(NullLogger<LiveEventBroadcaster>.Instance);

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var broadcaster = CreateBroadcaster();
        var evt = new LiveEvent(LiveEventType.Observation, new { id = "1" });
        var ex = Record.Exception(() => broadcaster.Publish(evt));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Subscriber_ReceivesPublishedEvent()
    {
        var broadcaster = CreateBroadcaster();
        var evt = new LiveEvent(LiveEventType.Observation, new { id = "1" });

        using var cts = new CancellationTokenSource();
        var received = new List<LiveEvent>();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
            {
                received.Add(e);
                cts.Cancel();
            }
        });

        // Give subscriber time to register
        await Task.Delay(50);
        broadcaster.Publish(evt);

        await Task.WhenAny(subscribeTask, Task.Delay(2000));
        Assert.Single(received);
        Assert.Equal(LiveEventType.Observation, received[0].EventType);
    }

    [Fact]
    public async Task MultipleSubscribers_EachReceiveEvent()
    {
        var broadcaster = CreateBroadcaster();
        var evt = new LiveEvent(LiveEventType.SpeedAlert, new { id = "2" });

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        var received1 = new List<LiveEvent>();
        var received2 = new List<LiveEvent>();

        var t1 = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts1.Token))
            {
                received1.Add(e);
                cts1.Cancel();
            }
        });
        var t2 = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts2.Token))
            {
                received2.Add(e);
                cts2.Cancel();
            }
        });

        await Task.Delay(100);
        broadcaster.Publish(evt);

        await Task.WhenAny(Task.WhenAll(t1, t2), Task.Delay(2000));
        Assert.Single(received1);
        Assert.Single(received2);
    }

    [Fact]
    public async Task Subscriber_RemovedOnCancellation_ReceivesNoMoreEvents()
    {
        var broadcaster = CreateBroadcaster();
        using var cts = new CancellationTokenSource();
        var received = new List<LiveEvent>();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
            {
                received.Add(e);
            }
        });

        await Task.Delay(50);
        cts.Cancel();
        await Task.WhenAny(subscribeTask, Task.Delay(1000));

        // After cancellation, publish should not add to received
        broadcaster.Publish(new LiveEvent(LiveEventType.GeofenceBreach, new { id = "3" }));
        await Task.Delay(50);

        Assert.Empty(received);
    }

    [Fact]
    public void Publish_IsNonBlocking_TryWriteSemantics()
    {
        var broadcaster = CreateBroadcaster();
        // Fill the channel beyond capacity - should not block
        var completed = false;
        var task = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                broadcaster.Publish(new LiveEvent(LiveEventType.Observation, new { id = i }));
            }
            completed = true;
        });

        task.Wait(TimeSpan.FromSeconds(2));
        Assert.True(completed, "Publish should be non-blocking even when channel is full");
    }
}
