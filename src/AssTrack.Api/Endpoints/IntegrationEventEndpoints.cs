using AssTrack.Api.Auth;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace AssTrack.Api.Endpoints;

public static class IntegrationEventEndpoints
{
    public static RouteGroupBuilder MapIntegrationEventEndpoints(this RouteGroupBuilder group)
    {
        var events = group.MapGroup("/integration-events").RequireAuthorization(AssTrackPolicies.Operator);

        events.MapGet(string.Empty, async (
            IntegrationEventRepository repository,
            string? source,
            string? externalEventId,
            string? eventType,
            string? severity,
            string? status,
            string? subjectType,
            string? subjectId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 200);
            var (items, totalCount) = await repository.SearchAsync(
                source,
                externalEventId,
                eventType,
                severity,
                status,
                subjectType,
                subjectId,
                from?.UtcDateTime,
                to?.UtcDateTime,
                p,
                ps,
                cancellationToken);

            return Results.Ok(new PagedResult<IntegrationEventDto>(
                items.Select(IntegrationEventService.Map).ToArray(),
                totalCount,
                p,
                ps));
        })
        .WithName("GetIntegrationEvents")
        .WithSummary("Search enterprise integration signals and events.");

        events.MapGet("/export", async (
            IntegrationEventRepository repository,
            string? source,
            string? externalEventId,
            string? eventType,
            string? severity,
            string? status,
            string? subjectType,
            string? subjectId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken) =>
        {
            var items = await repository.ExportAsync(
                source,
                externalEventId,
                eventType,
                severity,
                status,
                subjectType,
                subjectId,
                from?.UtcDateTime,
                to?.UtcDateTime,
                cancellationToken: cancellationToken);

            return Results.Content(BuildIntegrationEventCsv(items), "text/csv", Encoding.UTF8);
        })
        .WithName("ExportIntegrationEvents")
        .WithSummary("Export enterprise integration signals as CSV.");

        events.MapPost(string.Empty, async (
            [FromBody] CreateIntegrationEventRequest request,
            IIntegrationEventService eventService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var integrationEvent = await eventService.PublishAsync(request, httpContext, cancellationToken);
                return Results.Created($"/api/integration-events/{integrationEvent.Id}", IntegrationEventService.Map(integrationEvent));
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [ex.ParamName ?? "request"] = [ex.Message]
                });
            }
        })
        .WithName("PublishIntegrationEvent")
        .WithSummary("Publish an enterprise integration signal into hooks, live events, audit, and messaging routes.");

        events.MapPost("/{id:guid}/acknowledge", async (
            Guid id,
            IntegrationEventRepository repository,
            IAuditService audit,
            ILiveEventBroadcaster broadcaster,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(httpContext);
            var integrationEvent = await repository.AcknowledgeAsync(id, actor, DateTime.UtcNow, cancellationToken);
            if (integrationEvent is null) return Results.NotFound();

            broadcaster.Publish(new LiveEvent(LiveEventType.IntegrationEvent, IntegrationEventService.Map(integrationEvent)));
            await audit.RecordAsync(
                httpContext,
                "integration_event.acknowledged",
                "integration_event",
                integrationEvent.Id.ToString(),
                integrationEvent.SubjectName ?? integrationEvent.EventType,
                $"Acknowledged integration event {integrationEvent.EventType} from {integrationEvent.Source}.",
                new { integrationEvent.Source, integrationEvent.EventType, integrationEvent.Severity, integrationEvent.Status },
                cancellationToken);

            return Results.Ok(IntegrationEventService.Map(integrationEvent));
        })
        .WithName("AcknowledgeIntegrationEvent")
        .WithSummary("Acknowledge an open enterprise integration signal.");

        events.MapPost("/{id:guid}/resolve", async (
            Guid id,
            [FromBody] ResolveIntegrationEventRequest? request,
            IntegrationEventRepository repository,
            IAuditService audit,
            ILiveEventBroadcaster broadcaster,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(httpContext);
            var note = NormalizeResolutionNote(request?.ResolutionNote);
            var integrationEvent = await repository.ResolveAsync(id, actor, DateTime.UtcNow, note, cancellationToken);
            if (integrationEvent is null) return Results.NotFound();

            broadcaster.Publish(new LiveEvent(LiveEventType.IntegrationEvent, IntegrationEventService.Map(integrationEvent)));
            await audit.RecordAsync(
                httpContext,
                "integration_event.resolved",
                "integration_event",
                integrationEvent.Id.ToString(),
                integrationEvent.SubjectName ?? integrationEvent.EventType,
                $"Resolved integration event {integrationEvent.EventType} from {integrationEvent.Source}.",
                new { integrationEvent.Source, integrationEvent.EventType, integrationEvent.Severity, integrationEvent.Status, integrationEvent.ResolutionNote },
                cancellationToken);

            return Results.Ok(IntegrationEventService.Map(integrationEvent));
        })
        .WithName("ResolveIntegrationEvent")
        .WithSummary("Resolve an enterprise integration signal.");

        return group;
    }

    private static string ResolveActor(HttpContext httpContext)
        => httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue(ClaimTypes.Name)
            ?? "unknown";

    private static string? NormalizeResolutionNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }

    private static string BuildIntegrationEventCsv(IReadOnlyList<IntegrationEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,occurredAt,source,externalEventId,eventType,severity,status,subjectType,subjectId,subjectName,message,payloadJson,correlationId,acknowledgedAt,acknowledgedBy,resolvedAt,resolvedBy,resolutionNote");
        foreach (var integrationEvent in events)
        {
            sb.AppendLine(string.Join(",",
                integrationEvent.Id,
                ApiDateTime.Utc(integrationEvent.OccurredAt).ToString("O"),
                Csv(integrationEvent.Source),
                Csv(integrationEvent.ExternalEventId),
                Csv(integrationEvent.EventType),
                Csv(integrationEvent.Severity),
                Csv(integrationEvent.Status),
                Csv(integrationEvent.SubjectType),
                Csv(integrationEvent.SubjectId),
                Csv(integrationEvent.SubjectName),
                Csv(integrationEvent.Message),
                Csv(integrationEvent.PayloadJson),
                Csv(integrationEvent.CorrelationId),
                ApiDateTime.Utc(integrationEvent.AcknowledgedAt)?.ToString("O") ?? string.Empty,
                Csv(integrationEvent.AcknowledgedBy),
                ApiDateTime.Utc(integrationEvent.ResolvedAt)?.ToString("O") ?? string.Empty,
                Csv(integrationEvent.ResolvedBy),
                Csv(integrationEvent.ResolutionNote)));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
