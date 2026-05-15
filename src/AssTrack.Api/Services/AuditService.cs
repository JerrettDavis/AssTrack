using System.Security.Claims;
using System.Text.Json;
using AssTrack.Api.Auth;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Services;

public interface IAuditService
{
    Task RecordAsync(
        HttpContext httpContext,
        string action,
        string entityType,
        string? entityId = null,
        string? entityName = null,
        string? summary = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}

public class AuditService(AuditEventRepository repository) : IAuditService
{
    public Task RecordAsync(
        HttpContext httpContext,
        string action,
        string entityType,
        string? entityId = null,
        string? entityName = null,
        string? summary = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var actorName = httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue(ClaimTypes.Name)
            ?? "unknown";
        var actorRole = GetEffectiveRole(httpContext.User);

        return repository.AddAsync(new AuditEvent
        {
            ActorName = actorName,
            ActorRole = actorRole,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Summary = summary,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            CorrelationId = httpContext.Response.Headers["X-Correlation-Id"].ToString()
        }, cancellationToken);
    }

    private static string GetEffectiveRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(AssTrackRoles.Admin)) return AssTrackRoles.Admin;
        if (user.IsInRole(AssTrackRoles.Operator)) return AssTrackRoles.Operator;
        if (user.IsInRole(AssTrackRoles.Viewer)) return AssTrackRoles.Viewer;
        if (user.IsInRole(AssTrackRoles.Ingest)) return AssTrackRoles.Ingest;
        return "unknown";
    }
}
