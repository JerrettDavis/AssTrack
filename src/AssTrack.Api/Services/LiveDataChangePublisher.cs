using AssTrack.Api;

namespace AssTrack.Api.Services;

public static class LiveDataChangePublisher
{
    public static void PublishDataChanged(this ILiveEventBroadcaster broadcaster, string entity, string action, Guid? id = null, object? metadata = null)
    {
        broadcaster.Publish(new LiveEvent(LiveEventType.DataChanged, new
        {
            entity,
            action,
            id,
            metadata,
            occurredAt = ApiDateTime.Utc(DateTime.UtcNow)
        }));
    }
}
