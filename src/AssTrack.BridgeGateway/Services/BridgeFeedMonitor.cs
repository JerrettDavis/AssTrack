using System.Collections.Concurrent;

namespace AssTrack.BridgeGateway.Services;

public sealed class BridgeFeedMonitor
{
    private readonly ConcurrentDictionary<string, BridgeFeedRuntimeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<BridgeFeedLogEntry> _logs = new();
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
