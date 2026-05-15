using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Api.Services;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AssTrack.Api.Endpoints;

public static class AlertRoutingEndpoints
{
    public static RouteGroupBuilder MapAlertRoutingEndpoints(this RouteGroupBuilder group)
    {
        var routes = group.MapGroup("/alert-routes").RequireAuthorization("Operator");

        routes.MapGet(string.Empty, async (AlertRoutingRuleRepository repository, CancellationToken cancellationToken) =>
        {
            var items = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(items.Select(Map));
        });

        routes.MapPost(string.Empty, async (
            [FromBody] CreateAlertRoutingRuleRequest request,
            AlertRoutingRuleRepository repository,
            IntegrationFeedRepository integrationFeeds,
            AssetRepository assets,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.Name, request.EventType, request.Channel, request.Provider, request.AssetId, request.IntegrationFeedId, assets, integrationFeeds, cancellationToken);
            if (validation is not null) return validation;

            var now = DateTime.UtcNow;
            var rule = await repository.AddAsync(new AlertRoutingRule
            {
                Name = request.Name.Trim(),
                IsEnabled = request.IsEnabled,
                EventType = NormalizeEventType(request.EventType),
                Channel = request.Channel.Trim(),
                Provider = request.Provider.Trim(),
                AssetId = request.AssetId,
                IntegrationFeedId = request.IntegrationFeedId,
                ExternalPeerId = NormalizeNullable(request.ExternalPeerId),
                DisplayName = NormalizeNullable(request.DisplayName),
                Recipient = NormalizeNullable(request.Recipient),
                MessageTemplate = NormalizeNullable(request.MessageTemplate),
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);

            broadcaster.PublishDataChanged("alert_route", "created", rule.Id);
            await audit.RecordAsync(
                httpContext,
                "alert_route.created",
                "alert_route",
                rule.Id.ToString(),
                rule.Name,
                $"Created alert route {rule.Name}.",
                new { rule.EventType, rule.Provider, rule.AssetId, rule.IntegrationFeedId },
                cancellationToken);
            return Results.Created($"/api/alert-routes/{rule.Id}", Map(rule));
        });

        routes.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAlertRoutingRuleRequest request,
            AlertRoutingRuleRepository repository,
            IntegrationFeedRepository integrationFeeds,
            AssetRepository assets,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.Name, request.EventType, request.Channel, request.Provider, request.AssetId, request.IntegrationFeedId, assets, integrationFeeds, cancellationToken);
            if (validation is not null) return validation;

            var updated = await repository.UpdateAsync(
                id,
                request.Name.Trim(),
                request.IsEnabled,
                NormalizeEventType(request.EventType),
                request.Channel.Trim(),
                request.Provider.Trim(),
                request.AssetId,
                request.IntegrationFeedId,
                NormalizeNullable(request.ExternalPeerId),
                NormalizeNullable(request.DisplayName),
                NormalizeNullable(request.Recipient),
                NormalizeNullable(request.MessageTemplate),
                cancellationToken);

            if (updated is not null)
            {
                broadcaster.PublishDataChanged("alert_route", "updated", updated.Id);
                await audit.RecordAsync(
                    httpContext,
                    "alert_route.updated",
                    "alert_route",
                    updated.Id.ToString(),
                    updated.Name,
                    $"Updated alert route {updated.Name}.",
                    new { updated.EventType, updated.Provider, updated.AssetId, updated.IntegrationFeedId, updated.IsEnabled },
                    cancellationToken);
            }
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        });

        routes.MapDelete("/{id:guid}", async (
            Guid id,
            AlertRoutingRuleRepository repository,
            IAuditService audit,
            HttpContext httpContext,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            var existing = await repository.GetByIdAsync(id, cancellationToken);
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            if (deleted)
            {
                broadcaster.PublishDataChanged("alert_route", "deleted", id);
                await audit.RecordAsync(
                    httpContext,
                    "alert_route.deleted",
                    "alert_route",
                    id.ToString(),
                    existing?.Name,
                    existing is null ? "Deleted alert route." : $"Deleted alert route {existing.Name}.",
                    new { existing?.EventType, existing?.Provider, existing?.AssetId, existing?.IntegrationFeedId },
                    cancellationToken);
            }
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    private static async Task<IResult?> ValidateAsync(
        string name,
        string eventType,
        string channel,
        string provider,
        Guid? assetId,
        Guid? integrationFeedId,
        AssetRepository assets,
        IntegrationFeedRepository integrationFeeds,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        if (!IsValidEventType(eventType)) errors["eventType"] = ["Event type must be all, speed_alert, geofence_breach, or enterprise_signal."];
        if (string.IsNullOrWhiteSpace(channel)) errors["channel"] = ["Channel is required."];
        if (string.IsNullOrWhiteSpace(provider)) errors["provider"] = ["Provider is required."];
        if (assetId.HasValue && await assets.GetByIdAsync(assetId.Value, cancellationToken) is null)
        {
            errors["assetId"] = ["Asset was not found."];
        }
        if (integrationFeedId.HasValue && await integrationFeeds.GetByIdAsync(integrationFeedId.Value, cancellationToken) is null)
        {
            errors["integrationFeedId"] = ["Integration feed was not found."];
        }

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static AlertRoutingRuleDto Map(AlertRoutingRule rule)
        => new(
            rule.Id,
            rule.Name,
            rule.IsEnabled,
            rule.EventType,
            rule.Channel,
            rule.Provider,
            rule.AssetId,
            rule.Asset?.Name,
            rule.IntegrationFeedId,
            rule.IntegrationFeed?.Name,
            rule.ExternalPeerId,
            rule.DisplayName,
            rule.Recipient,
            rule.MessageTemplate,
            ApiDateTime.Utc(rule.CreatedAt),
            ApiDateTime.Utc(rule.UpdatedAt));

    private static bool IsValidEventType(string? value)
        => string.Equals(value, AlertRouteEventTypes.All, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, AlertRouteEventTypes.SpeedAlert, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, AlertRouteEventTypes.GeofenceBreach, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, AlertRouteEventTypes.EnterpriseSignal, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEventType(string value)
        => value.Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
