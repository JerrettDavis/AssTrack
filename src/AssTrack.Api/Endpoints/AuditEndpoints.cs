using AssTrack.Api.Auth;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using System.Text;

namespace AssTrack.Api.Endpoints;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        var audit = group.MapGroup("/audit-events").RequireAuthorization(AssTrackPolicies.Admin);

        audit.MapGet(string.Empty, async (
            AuditEventRepository repository,
            string? action,
            string? entityType,
            string? actor,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 200);
            var (items, totalCount) = await repository.SearchAsync(
                action,
                entityType,
                actor,
                from?.UtcDateTime,
                to?.UtcDateTime,
                p,
                ps,
                cancellationToken);

            return Results.Ok(new PagedResult<AuditEventDto>(
                items.Select(Map).ToArray(),
                totalCount,
                p,
                ps));
        })
        .WithName("GetAuditEvents")
        .WithSummary("Search enterprise audit events.");

        audit.MapGet("/export", async (
            AuditEventRepository repository,
            string? action,
            string? entityType,
            string? actor,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken) =>
        {
            var items = await repository.ExportAsync(
                action,
                entityType,
                actor,
                from?.UtcDateTime,
                to?.UtcDateTime,
                cancellationToken: cancellationToken);

            return Results.Content(BuildAuditCsv(items), "text/csv", Encoding.UTF8);
        })
        .WithName("ExportAuditEvents")
        .WithSummary("Export enterprise audit events as CSV.");

        return group;
    }

    private static AuditEventDto Map(AuditEvent auditEvent)
        => new(
            auditEvent.Id,
            ApiDateTime.Utc(auditEvent.OccurredAt),
            auditEvent.ActorName,
            auditEvent.ActorRole,
            auditEvent.Action,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.EntityName,
            auditEvent.Summary,
            auditEvent.MetadataJson,
            auditEvent.CorrelationId);

    private static string BuildAuditCsv(IReadOnlyList<AuditEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,occurredAt,actorName,actorRole,action,entityType,entityId,entityName,summary,metadataJson,correlationId");
        foreach (var auditEvent in events)
        {
            sb.AppendLine(string.Join(",",
                auditEvent.Id,
                ApiDateTime.Utc(auditEvent.OccurredAt).ToString("O"),
                Csv(auditEvent.ActorName),
                Csv(auditEvent.ActorRole),
                Csv(auditEvent.Action),
                Csv(auditEvent.EntityType),
                Csv(auditEvent.EntityId),
                Csv(auditEvent.EntityName),
                Csv(auditEvent.Summary),
                Csv(auditEvent.MetadataJson),
                Csv(auditEvent.CorrelationId)));
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
