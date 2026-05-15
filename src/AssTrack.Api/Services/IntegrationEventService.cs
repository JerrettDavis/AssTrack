using System.Text.Json;
using AssTrack.Api;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Services;

public interface IIntegrationEventService
{
    Task<IntegrationEvent> PublishAsync(CreateIntegrationEventRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);
}

public sealed class IntegrationEventService(
    IntegrationEventRepository repository,
    ILiveEventBroadcaster broadcaster,
    IWebhookNotificationService webhookNotificationService,
    IAlertRoutingService alertRoutingService,
    IAuditService auditService) : IIntegrationEventService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntegrationEvent> PublishAsync(
        CreateIntegrationEventRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].ToString();
        var source = NormalizeRequired(request.Source, "source", 120);
        var externalEventId = NormalizeNullable(request.ExternalEventId, 200);
        if (externalEventId is not null)
        {
            var existing = await repository.GetBySourceExternalEventIdAsync(source, externalEventId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var integrationEvent = await repository.AddAsync(new IntegrationEvent
        {
            Source = source,
            ExternalEventId = externalEventId,
            EventType = NormalizeRequired(request.EventType, "eventType", 120),
            Severity = NormalizeSeverity(request.Severity),
            SubjectType = NormalizeNullable(request.SubjectType, 120),
            SubjectId = NormalizeNullable(request.SubjectId, 120),
            SubjectName = NormalizeNullable(request.SubjectName, 300),
            Message = NormalizeRequired(request.Message, "message", 1000),
            PayloadJson = request.Payload is null ? null : JsonSerializer.Serialize(request.Payload, JsonOptions),
            OccurredAt = ApiDateTime.Utc(request.OccurredAt ?? DateTime.UtcNow),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId
        }, cancellationToken);

        broadcaster.Publish(new LiveEvent(LiveEventType.IntegrationEvent, Map(integrationEvent)));
        await webhookNotificationService.NotifyIntegrationEventAsync(integrationEvent, cancellationToken);
        await alertRoutingService.RouteIntegrationEventAsync(integrationEvent, cancellationToken);
        await auditService.RecordAsync(
            httpContext,
            "integration_event.published",
            "integration_event",
            integrationEvent.Id.ToString(),
            integrationEvent.SubjectName ?? integrationEvent.EventType,
            $"Published integration event {integrationEvent.EventType} from {integrationEvent.Source}.",
            new { integrationEvent.Source, integrationEvent.ExternalEventId, integrationEvent.EventType, integrationEvent.Severity, integrationEvent.SubjectType, integrationEvent.SubjectId },
            cancellationToken);

        return integrationEvent;
    }

    public static IntegrationEventDto Map(IntegrationEvent integrationEvent)
        => new(
            integrationEvent.Id,
            ApiDateTime.Utc(integrationEvent.OccurredAt),
            integrationEvent.Source,
            integrationEvent.ExternalEventId,
            integrationEvent.EventType,
            integrationEvent.Severity,
            integrationEvent.SubjectType,
            integrationEvent.SubjectId,
            integrationEvent.SubjectName,
            integrationEvent.Message,
            integrationEvent.PayloadJson,
            integrationEvent.CorrelationId,
            integrationEvent.Status,
            ApiDateTime.Utc(integrationEvent.AcknowledgedAt),
            integrationEvent.AcknowledgedBy,
            ApiDateTime.Utc(integrationEvent.ResolvedAt),
            integrationEvent.ResolvedBy,
            integrationEvent.ResolutionNote);

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return IntegrationEventSeverities.Info;

        return severity.Trim().ToLowerInvariant() switch
        {
            IntegrationEventSeverities.Info => IntegrationEventSeverities.Info,
            IntegrationEventSeverities.Warning => IntegrationEventSeverities.Warning,
            IntegrationEventSeverities.Critical => IntegrationEventSeverities.Critical,
            _ => IntegrationEventSeverities.Info
        };
    }
}
