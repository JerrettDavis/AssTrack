using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AssTrack.Api.Services;

public sealed class LiveEventBroadcaster(ILogger<LiveEventBroadcaster> logger) : ILiveEventBroadcaster
{
    private readonly List<Channel<LiveEvent>> _subscribers = new();
    private readonly Lock _lock = new();

    public void Publish(LiveEvent evt)
    {
        List<Channel<LiveEvent>> snapshot;
        lock (_lock)
        {
            snapshot = [.. _subscribers];
        }
        foreach (var channel in snapshot)
        {
            if (!channel.Writer.TryWrite(evt))
            {
                logger.LogWarning("LiveEventBroadcaster: channel full, dropping event {EventType}", evt.EventType);
            }
        }
    }

    public IAsyncEnumerable<LiveEvent> SubscribeAsync(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<LiveEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        cancellationToken.Register(() =>
        {
            lock (_lock) { _subscribers.Remove(channel); }
            channel.Writer.TryComplete();
        }, useSynchronizationContext: false);

        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
