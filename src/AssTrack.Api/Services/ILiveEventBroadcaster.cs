namespace AssTrack.Api.Services;

public enum LiveEventType { Observation, SpeedAlert, GeofenceBreach, Message }

public record LiveEvent(LiveEventType EventType, object Payload);

public interface ILiveEventBroadcaster
{
    void Publish(LiveEvent evt);
    IAsyncEnumerable<LiveEvent> SubscribeAsync(CancellationToken cancellationToken);
}
