using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
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
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.Name, request.EventType, request.Channel, request.Provider, request.IntegrationFeedId, integrationFeeds, cancellationToken);
            if (validation is not null) return validation;

            var now = DateTime.UtcNow;
            var rule = await repository.AddAsync(new AlertRoutingRule
            {
                Name = request.Name.Trim(),
                IsEnabled = request.IsEnabled,
                EventType = NormalizeEventType(request.EventType),
                Channel = request.Channel.Trim(),
                Provider = request.Provider.Trim(),
                IntegrationFeedId = request.IntegrationFeedId,
                ExternalPeerId = NormalizeNullable(request.ExternalPeerId),
                DisplayName = NormalizeNullable(request.DisplayName),
                Recipient = NormalizeNullable(request.Recipient),
                MessageTemplate = NormalizeNullable(request.MessageTemplate),
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);

            return Results.Created($"/api/alert-routes/{rule.Id}", Map(rule));
        });

        routes.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAlertRoutingRuleRequest request,
            AlertRoutingRuleRepository repository,
            IntegrationFeedRepository integrationFeeds,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request.Name, request.EventType, request.Channel, request.Provider, request.IntegrationFeedId, integrationFeeds, cancellationToken);
            if (validation is not null) return validation;

            var updated = await repository.UpdateAsync(
                id,
                request.Name.Trim(),
                request.IsEnabled,
                NormalizeEventType(request.EventType),
                request.Channel.Trim(),
                request.Provider.Trim(),
                request.IntegrationFeedId,
                NormalizeNullable(request.ExternalPeerId),
                NormalizeNullable(request.DisplayName),
                NormalizeNullable(request.Recipient),
                NormalizeNullable(request.MessageTemplate),
                cancellationToken);

            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        });

        routes.MapDelete("/{id:guid}", async (Guid id, AlertRoutingRuleRepository repository, CancellationToken cancellationToken) =>
            await repository.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        return group;
    }

    private static async Task<IResult?> ValidateAsync(
        string name,
        string eventType,
        string channel,
        string provider,
        Guid? integrationFeedId,
        IntegrationFeedRepository integrationFeeds,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        if (!IsValidEventType(eventType)) errors["eventType"] = ["Event type must be all, speed_alert, or geofence_breach."];
        if (string.IsNullOrWhiteSpace(channel)) errors["channel"] = ["Channel is required."];
        if (string.IsNullOrWhiteSpace(provider)) errors["provider"] = ["Provider is required."];
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
           string.Equals(value, AlertRouteEventTypes.GeofenceBreach, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEventType(string value)
        => value.Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
