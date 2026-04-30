using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Endpoints;

public static class WebhookEndpoints
{
    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        var webhooks = group.MapGroup("/webhooks");

        webhooks.MapGet("/deliveries", async (
            AssTrackDbContext db,
            bool? success,
            string? eventType,
            DateTime? since,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = db.WebhookDeliveryLogs.AsQueryable();

            if (success.HasValue)
                query = query.Where(x => x.Success == success.Value);
            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(x => x.EventType == eventType);
            if (since.HasValue)
                query = query.Where(x => x.AttemptedAt >= since.Value);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(x => x.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new WebhookDeliveryLogDto(
                    x.Id,
                    x.AttemptedAt,
                    x.EventType,
                    x.TargetUrl,
                    x.Success,
                    x.HttpStatusCode,
                    x.DurationMs,
                    x.ErrorMessage,
                    x.RequestPayloadSummary))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<WebhookDeliveryLogDto>(items, total, page, pageSize));
        })
        .WithName("GetWebhookDeliveries")
        .WithSummary("List webhook delivery logs with optional filtering.");

        webhooks.MapGet("/status", async (
            AssTrackDbContext db,
            IOptions<WebhookOptions> webhookOptions,
            CancellationToken ct) =>
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);

            var last24h = await db.WebhookDeliveryLogs
                .Where(x => x.AttemptedAt >= cutoff)
                .Select(x => new { x.Success, x.DurationMs, x.AttemptedAt })
                .ToListAsync(ct);

            var lastDeliveredAt = last24h.Count > 0
                ? last24h.Max(x => x.AttemptedAt)
                : await db.WebhookDeliveryLogs
                    .OrderByDescending(x => x.AttemptedAt)
                    .Select(x => (DateTime?)x.AttemptedAt)
                    .FirstOrDefaultAsync(ct);

            var avgDurationMs = last24h.Count > 0
                ? last24h.Average(x => (double)x.DurationMs)
                : (double?)null;

            return Results.Ok(new WebhookStatusDto(
                Configured: !string.IsNullOrWhiteSpace(webhookOptions.Value.Url),
                Last24hDeliveries: last24h.Count,
                Last24hFailures: last24h.Count(x => !x.Success),
                LastDeliveredAt: lastDeliveredAt,
                AvgDurationMs: avgDurationMs));
        })
        .WithName("GetWebhookStatus")
        .WithSummary("Get webhook configuration status and 24-hour delivery statistics.");

        webhooks.MapPost("/test", async (
            [FromBody] TestWebhookRequest? body,
            IWebhookNotificationService webhookService,
            IOptions<WebhookOptions> webhookOptions,
            CancellationToken ct) =>
        {
            var eventType = string.IsNullOrWhiteSpace(body?.EventType) ? "speed_alert" : body.EventType;
            var configured = !string.IsNullOrWhiteSpace(webhookOptions.Value.Url);

            if (eventType == "geofence_breach")
            {
                var breach = new GeofenceBreach
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    AssetId = null,
                    GeofenceId = Guid.NewGuid(),
                    ObservationId = Guid.Empty,
                    EventType = GeofenceBreachEventType.Enter,
                    DetectedAt = DateTime.UtcNow
                };
                await webhookService.NotifyGeofenceBreachAsync(breach, ct);
            }
            else
            {
                var alert = new SpeedAlert
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    AssetId = null,
                    ObservationId = Guid.Empty,
                    ObservedSpeedKmh = 120,
                    ThresholdKmh = 100,
                    TriggeredAt = DateTime.UtcNow
                };
                await webhookService.NotifySpeedAlertAsync(alert, ct);
            }

            await Task.Delay(100, ct);

            var message = configured
                ? "Test webhook event sent. Check delivery logs for outcome."
                : "No webhook URL configured. Test event processed but no HTTP request was made.";

            return Results.Ok(new TestWebhookFireResponse(
                Fired: true,
                EventType: eventType,
                Configured: configured,
                Message: message));
        })
        .WithName("TestWebhookFire")
        .WithSummary("Fire a synthetic test webhook event to verify delivery configuration.");

        return group;
    }
}
