using System.Collections.Concurrent;

namespace AssTrack.BridgeGateway.Services;

public sealed class BridgeFeedMonitor
{
    private readonly ConcurrentDictionary<string, BridgeFeedRuntimeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<BridgeFeedLogEntry> _logs = new();
    private readonly ConcurrentQueue<BridgeRawMessageEntry> _messages = new();
    private readonly ConcurrentDictionary<string, int> _resyncRequests = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BridgeFeedRuntimeStatus> Statuses => _statuses.Values
        .OrderBy(x => x.FeedKey, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<BridgeFeedLogEntry> Logs(string? feedKey = null, int limit = 100)
    {
        var query = _logs.Reverse();
        if (!string.IsNullOrWhiteSpace(feedKey))
        {
            query = query.Where(x => string.Equals(x.FeedKey, feedKey, StringComparison.OrdinalIgnoreCase));
        }

        return query.Take(Math.Clamp(limit, 1, 500)).Reverse().ToArray();
    }

    public IReadOnlyList<BridgeRawMessageEntry> Messages(
        string? feedKey = null,
        string? search = null,
        string? trackerId = null,
        string? topic = null,
        string? messageType = null,
        bool payloadOnly = false,
        int limit = 100)
    {
        var query = _messages.Reverse();
        if (!string.IsNullOrWhiteSpace(feedKey))
        {
            query = query.Where(x => string.Equals(x.FeedKey, feedKey, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(trackerId))
        {
            var term = trackerId.Trim();
            query = query.Where(x => Contains(x.TrackerId, term) || Contains(x.Payload, term));
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var term = topic.Trim();
            query = query.Where(x => Contains(x.Topic, term));
        }

        if (!string.IsNullOrWhiteSpace(messageType))
        {
            var term = messageType.Trim();
            query = query.Where(x => Contains(x.MessageType, term) || Contains(x.Payload, term));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = payloadOnly
                ? query.Where(x => Contains(x.Payload, term))
                : query.Where(x =>
                    Contains(x.FeedKey, term) ||
                    Contains(x.Provider, term) ||
                    Contains(x.Topic, term) ||
                    Contains(x.TrackerId, term) ||
                    Contains(x.MessageType, term) ||
                    Contains(x.Summary, term) ||
                    Contains(x.Payload, term));
        }

        return query.Take(Math.Clamp(limit, 1, 300)).Reverse().ToArray();
    }

    public int GetResyncVersion(string feedKey)
        => _resyncRequests.TryGetValue(feedKey, out var version) ? version : 0;

    public int RequestResync(string feedKey)
    {
        var version = _resyncRequests.AddOrUpdate(feedKey, 1, (_, current) => current + 1);
        Log(feedKey, "info", "Resync requested from UI.");
        Update(feedKey, status =>
        {
            status.State = "resync-requested";
            status.LastError = null;
        });
        return version;
    }

    public void Update(string feedKey, Action<BridgeFeedRuntimeStatus> update)
    {
        var status = _statuses.GetOrAdd(feedKey, key => new BridgeFeedRuntimeStatus { FeedKey = key });
        lock (status)
        {
            update(status);
            status.UpdatedAt = DateTime.UtcNow;
        }
    }

    public void Log(string feedKey, string level, string message)
    {
        _logs.Enqueue(new BridgeFeedLogEntry(DateTime.UtcNow, feedKey, level, message));
        while (_logs.Count > 1000 && _logs.TryDequeue(out _))
        {
        }
    }

    public void RecordMessage(
        string feedKey,
        string provider,
        string? topic,
        string? trackerId,
        string? messageType,
        string summary,
        string payload)
    {
        _messages.Enqueue(new BridgeRawMessageEntry(
            DateTime.UtcNow,
            feedKey,
            provider,
            topic,
            trackerId,
            messageType,
            summary,
            Truncate(payload, 20_000)));

        while (_messages.Count > 1000 && _messages.TryDequeue(out _))
        {
        }
    }

    private static bool Contains(string? value, string search)
        => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : $"{value[..maxLength]}\n... truncated ...";
}

public sealed class BridgeFeedRuntimeStatus
{
    public string FeedKey { get; set; } = string.Empty;
    public Guid? FeedId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string State { get; set; } = "configured";
    public string? Host { get; set; }
    public string? Topic { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public string? LastTrackerId { get; set; }
    public string? LastError { get; set; }
    public int MessagesReceived { get; set; }
    public int ObservationsParsed { get; set; }
    public int ObservationsDelivered { get; set; }
    public int DeliveryFailures { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record BridgeFeedLogEntry(DateTime Timestamp, string FeedKey, string Level, string Message);

public sealed record BridgeRawMessageEntry(
    DateTime Timestamp,
    string FeedKey,
    string Provider,
    string? Topic,
    string? TrackerId,
    string? MessageType,
    string Summary,
    string Payload);
