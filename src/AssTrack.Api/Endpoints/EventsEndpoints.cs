using AssTrack.Api.Services;
using System.Text;
using System.Text.Json;

namespace AssTrack.Api.Endpoints;

public static class EventsEndpoints
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleSseAsync(HttpContext context, ILiveEventBroadcaster broadcaster, ISseTokenService tokenService, CancellationToken ct)
    {
        // Validate token from query parameter
        if (!context.Request.Query.TryGetValue("token", out var tokenValue) || string.IsNullOrWhiteSpace(tokenValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "SSE token is required" }, cancellationToken: ct);
            return;
        }

        if (!tokenService.ValidateToken(tokenValue.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired SSE token" }, cancellationToken: ct);
            return;
        }

        var response = context.Response;
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        response.Headers["Connection"] = "keep-alive";

        await response.StartAsync(ct);

        // Subscribe immediately (channel registered before any write), then confirm stream is live.
        // The ": connected" comment also acts as a production heartbeat/keepalive.
        var events = broadcaster.SubscribeAsync(ct);
        await response.BodyWriter.WriteAsync(": connected\n\n"u8.ToArray(), ct);
        await response.BodyWriter.FlushAsync(ct);

        await foreach (var evt in events)
        {
            var eventName = evt.EventType switch
            {
                LiveEventType.Observation => "observation",
                LiveEventType.SpeedAlert => "speed_alert",
                LiveEventType.GeofenceBreach => "geofence_breach",
                _ => "unknown"
            };
            var data = JsonSerializer.Serialize(evt.Payload, _jsonOptions);
            var message = $"event: {eventName}\ndata: {data}\n\n";
            await response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(message), ct);
            await response.BodyWriter.FlushAsync(ct);
        }
    }
}
