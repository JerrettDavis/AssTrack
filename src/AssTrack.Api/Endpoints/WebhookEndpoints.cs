using AssTrack.Api.Services;
using AssTrack.Api;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading.Channels;

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
                    ApiDateTime.Utc(x.AttemptedAt),
                    x.EventType,
                    x.TargetUrl,
                    x.Success,
                    x.HttpStatusCode,
                    x.DurationMs,
                    x.ErrorMessage,
                    x.RequestPayloadSummary,
                    x.AttemptNumber,
                    x.CorrelationId))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<WebhookDeliveryLogDto>(items, total, page, pageSize));
        })
        .WithName("GetWebhookDeliveries")
        .WithSummary("List webhook delivery logs with optional filtering.")
        .RequireAuthorization("Operator");

        webhooks.MapPost("/deliveries/{id:int}/replay", async (
            int id,
            AssTrackDbContext db,
            IWebhookNotificationService webhookService,
            WebhookSubscriptionRepository subscriptions,
            IAuditService audit,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var delivery = await db.WebhookDeliveryLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (delivery is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(delivery.RequestPayloadJson))
            {
                return Results.Problem(
                    title: "Delivery cannot be replayed.",
                    detail: "The original full payload was not retained for this delivery.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var subscription = await subscriptions.GetEnabledForTargetAsync(delivery.TargetUrl, delivery.EventType, ct);
            await webhookService.ExecuteRetryAsync(new WebhookRetryJob(
                WebhookId: Guid.NewGuid(),
                Payload: delivery.RequestPayloadJson,
                EventType: delivery.EventType,
                AttemptNumber: delivery.AttemptNumber + 1,
                CorrelationId: delivery.CorrelationId,
                ScheduledAt: DateTime.UtcNow)
            {
                TargetUrl = delivery.TargetUrl,
                SigningSecret = subscription?.SigningSecret
            }, ct);

            await audit.RecordAsync(
                httpContext,
                "webhook_delivery.replayed",
                "webhook_delivery",
                delivery.Id.ToString(),
                delivery.EventType,
                $"Replayed webhook delivery {delivery.Id} to {delivery.TargetUrl}.",
                new { delivery.EventType, delivery.TargetUrl, delivery.CorrelationId, ReplayAttemptNumber = delivery.AttemptNumber + 1 },
                ct);

            return Results.Ok(new WebhookReplayResponse(
                true,
                delivery.Id,
                delivery.EventType,
                delivery.TargetUrl,
                "Webhook delivery replayed. Check delivery logs for the replay attempt outcome."));
        })
        .WithName("ReplayWebhookDelivery")
        .WithSummary("Replay a retained webhook delivery payload to its original target.")
        .RequireAuthorization("Operator");

        webhooks.MapGet("/status", async (
            AssTrackDbContext db,
            IOptions<WebhookOptions> webhookOptions,
            ChannelReader<WebhookRetryJob> retryReader,
            WebhookSubscriptionRepository subscriptions,
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

            var enabledSubscriptions = await subscriptions.CountEnabledAsync(ct);

            return Results.Ok(new WebhookStatusDto(
                Configured: !string.IsNullOrWhiteSpace(webhookOptions.Value.Url) || enabledSubscriptions > 0,
                Last24hDeliveries: last24h.Count,
                Last24hFailures: last24h.Count(x => !x.Success),
                LastDeliveredAt: ApiDateTime.Utc(lastDeliveredAt),
                AvgDurationMs: avgDurationMs,
                RetryQueueDepth: retryReader.Count,
                SigningEnabled: !string.IsNullOrWhiteSpace(webhookOptions.Value.SigningSecret),
                EnabledSubscriptions: enabledSubscriptions));
        })
        .WithName("GetWebhookStatus")
        .WithSummary("Get webhook configuration status and 24-hour delivery statistics.")
        .RequireAuthorization("Operator");

        webhooks.MapPost("/test", async (
            [FromBody] TestWebhookRequest? body,
            IWebhookNotificationService webhookService,
            IOptions<WebhookOptions> webhookOptions,
            WebhookSubscriptionRepository subscriptions,
            CancellationToken ct) =>
        {
            var eventType = string.IsNullOrWhiteSpace(body?.EventType) ? "speed_alert" : body.EventType;
            var configured = !string.IsNullOrWhiteSpace(webhookOptions.Value.Url) ||
                (await subscriptions.GetEnabledForEventAsync(eventType, ct)).Count > 0;

            if (eventType == "enterprise_signal")
            {
                await webhookService.NotifyIntegrationEventAsync(new IntegrationEvent
                {
                    Id = Guid.NewGuid(),
                    Source = "webhook-test",
                    EventType = "test.signal",
                    Severity = IntegrationEventSeverities.Info,
                    SubjectType = "system",
                    SubjectId = "webhook-test",
                    SubjectName = "Webhook test",
                    Message = "Synthetic enterprise signal webhook test.",
                    PayloadJson = """{"test":true}""",
                    OccurredAt = DateTime.UtcNow
                }, ct);
            }
            else if (eventType == "geofence_breach")
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
        .WithSummary("Fire a synthetic test webhook event to verify delivery configuration.")
        .RequireAuthorization("Operator");

        webhooks.MapGet("/subscriptions", async (
            AssTrackDbContext db,
            WebhookSubscriptionRepository repository,
            CancellationToken ct) =>
        {
            var items = await repository.GetAllAsync(ct);
            return Results.Ok(await MapSubscriptionsAsync(db, items, ct));
        })
        .WithName("GetWebhookSubscriptions")
        .WithSummary("List event-specific webhook subscriptions.")
        .RequireAuthorization("Operator");

        webhooks.MapPost("/subscriptions", async (
            [FromBody] CreateWebhookSubscriptionRequest request,
            WebhookSubscriptionRepository repository,
            IAuditService audit,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var validation = ValidateSubscription(request.Name, request.EventTypes, request.TargetUrl);
            if (validation is not null) return validation;

            var now = DateTime.UtcNow;
            var subscription = await repository.AddAsync(new WebhookSubscription
            {
                Name = request.Name.Trim(),
                IsEnabled = request.IsEnabled,
                EventTypes = NormalizeEventTypes(request.EventTypes),
                TargetUrl = request.TargetUrl.Trim(),
                SigningSecret = NormalizeNullable(request.SigningSecret),
                CreatedAt = now,
                UpdatedAt = now
            }, ct);

            await audit.RecordAsync(
                httpContext,
                "webhook_subscription.created",
                "webhook_subscription",
                subscription.Id.ToString(),
                subscription.Name,
                $"Created webhook subscription {subscription.Name}.",
                new { subscription.EventTypes, subscription.TargetUrl, subscription.IsEnabled, SigningEnabled = !string.IsNullOrWhiteSpace(subscription.SigningSecret) },
                ct);

            return Results.Created($"/api/webhooks/subscriptions/{subscription.Id}", MapSubscription(subscription));
        })
        .WithName("CreateWebhookSubscription")
        .WithSummary("Create an event-specific webhook subscription.")
        .RequireAuthorization("Operator");

        webhooks.MapPost("/subscriptions/{id:guid}/test", async (
            Guid id,
            WebhookSubscriptionRepository repository,
            IWebhookNotificationService webhookService,
            IAuditService audit,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var subscription = await repository.GetByIdAsync(id, ct);
            if (subscription is null) return Results.NotFound();

            var now = DateTime.UtcNow;
            var correlationId = Guid.NewGuid().ToString("N");
            var payload = new IntegrationEventWebhookPayload(
                IntegrationEventTypes.EnterpriseSignal,
                Guid.NewGuid(),
                "subscription-test",
                "test.signal",
                IntegrationEventSeverities.Info,
                "webhook_subscription",
                subscription.Id.ToString(),
                subscription.Name,
                $"Synthetic webhook subscription test for {subscription.Name}.",
                """{"test":true}""",
                now,
                now,
                correlationId);

            await webhookService.ExecuteRetryAsync(new WebhookRetryJob(
                WebhookId: Guid.NewGuid(),
                Payload: JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                EventType: IntegrationEventTypes.EnterpriseSignal,
                AttemptNumber: 1,
                CorrelationId: correlationId,
                ScheduledAt: now)
            {
                TargetUrl = subscription.TargetUrl,
                SigningSecret = subscription.SigningSecret
            }, ct);

            await audit.RecordAsync(
                httpContext,
                "webhook_subscription.tested",
                "webhook_subscription",
                subscription.Id.ToString(),
                subscription.Name,
                $"Tested webhook subscription {subscription.Name}.",
                new { subscription.EventTypes, subscription.TargetUrl, subscription.IsEnabled, SigningEnabled = !string.IsNullOrWhiteSpace(subscription.SigningSecret) },
                ct);

            return Results.Ok(new WebhookSubscriptionTestResponse(
                true,
                subscription.Id,
                IntegrationEventTypes.EnterpriseSignal,
                subscription.TargetUrl,
                "Webhook subscription test sent. Check delivery logs for outcome."));
        })
        .WithName("TestWebhookSubscription")
        .WithSummary("Send a synthetic enterprise signal to one webhook subscription target.")
        .RequireAuthorization("Operator");

        webhooks.MapPut("/subscriptions/{id:guid}", async (
            Guid id,
            [FromBody] UpdateWebhookSubscriptionRequest request,
            WebhookSubscriptionRepository repository,
            IAuditService audit,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var validation = ValidateSubscription(request.Name, request.EventTypes, request.TargetUrl);
            if (validation is not null) return validation;

            var updated = await repository.UpdateAsync(
                id,
                request.Name.Trim(),
                request.IsEnabled,
                NormalizeEventTypes(request.EventTypes),
                request.TargetUrl.Trim(),
                NormalizeNullable(request.SigningSecret),
                ct);

            if (updated is not null)
            {
                await audit.RecordAsync(
                    httpContext,
                    "webhook_subscription.updated",
                    "webhook_subscription",
                    updated.Id.ToString(),
                    updated.Name,
                    $"Updated webhook subscription {updated.Name}.",
                    new { updated.EventTypes, updated.TargetUrl, updated.IsEnabled, SigningEnabled = !string.IsNullOrWhiteSpace(updated.SigningSecret) },
                    ct);
            }

            return updated is null ? Results.NotFound() : Results.Ok(MapSubscription(updated));
        })
        .WithName("UpdateWebhookSubscription")
        .WithSummary("Update an event-specific webhook subscription.")
        .RequireAuthorization("Operator");

        webhooks.MapDelete("/subscriptions/{id:guid}", async (
            Guid id,
            WebhookSubscriptionRepository repository,
            IAuditService audit,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await repository.GetByIdAsync(id, ct);
            var deleted = await repository.DeleteAsync(id, ct);
            if (deleted)
            {
                await audit.RecordAsync(
                    httpContext,
                    "webhook_subscription.deleted",
                    "webhook_subscription",
                    id.ToString(),
                    existing?.Name,
                    existing is null ? "Deleted webhook subscription." : $"Deleted webhook subscription {existing.Name}.",
                    new { existing?.EventTypes, existing?.TargetUrl, existing?.IsEnabled },
                    ct);
            }
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteWebhookSubscription")
        .WithSummary("Delete an event-specific webhook subscription.")
        .RequireAuthorization("Operator");

        return group;
    }

    private static async Task<IReadOnlyList<WebhookSubscriptionDto>> MapSubscriptionsAsync(
        AssTrackDbContext db,
        IReadOnlyList<WebhookSubscription> subscriptions,
        CancellationToken ct)
    {
        if (subscriptions.Count == 0) return [];

        var targetUrls = subscriptions.Select(x => x.TargetUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var deliveryLogs = await db.WebhookDeliveryLogs
            .AsNoTracking()
            .Where(x => targetUrls.Contains(x.TargetUrl))
            .OrderByDescending(x => x.AttemptedAt)
            .ToListAsync(ct);

        return subscriptions.Select(subscription => MapSubscription(
            subscription,
            deliveryLogs.Where(x => string.Equals(x.TargetUrl, subscription.TargetUrl, StringComparison.OrdinalIgnoreCase)).ToArray(),
            cutoff)).ToArray();
    }

    private static WebhookSubscriptionDto MapSubscription(WebhookSubscription subscription, IReadOnlyList<WebhookDeliveryLog>? deliveries = null, DateTime? cutoff = null)
    {
        var lastAttempt = deliveries?.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var lastSuccess = deliveries?.Where(x => x.Success).OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var lastFailure = deliveries?.Where(x => !x.Success).OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var recent = cutoff.HasValue
            ? deliveries?.Where(x => x.AttemptedAt >= cutoff.Value).ToArray() ?? []
            : [];
        var health = ResolveHealth(subscription, lastAttempt, recent);

        return new WebhookSubscriptionDto(
            subscription.Id,
            subscription.Name,
            subscription.IsEnabled,
            subscription.EventTypes,
            subscription.TargetUrl,
            !string.IsNullOrWhiteSpace(subscription.SigningSecret),
            ApiDateTime.Utc(subscription.CreatedAt),
            ApiDateTime.Utc(subscription.UpdatedAt),
            ApiDateTime.Utc(lastAttempt?.AttemptedAt),
            ApiDateTime.Utc(lastSuccess?.AttemptedAt),
            ApiDateTime.Utc(lastFailure?.AttemptedAt),
            lastAttempt?.HttpStatusCode,
            lastAttempt?.ErrorMessage,
            recent.Length,
            recent.Count(x => !x.Success),
            health);
    }

    private static string ResolveHealth(WebhookSubscription subscription, WebhookDeliveryLog? lastAttempt, IReadOnlyList<WebhookDeliveryLog> recent)
    {
        if (!subscription.IsEnabled) return "disabled";
        if (lastAttempt is null) return "idle";
        if (!lastAttempt.Success) return "failing";
        return recent.Any(x => !x.Success) ? "degraded" : "healthy";
    }

    private static IResult? ValidateSubscription(string name, string eventTypes, string targetUrl)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        if (string.IsNullOrWhiteSpace(eventTypes)) errors["eventTypes"] = ["At least one event type or * is required."];
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https")
        {
            errors["targetUrl"] = ["Target URL must be an absolute HTTP or HTTPS URL."];
        }

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static string NormalizeEventTypes(string value)
        => string.Join(",",
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .DefaultIfEmpty("*"));

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
