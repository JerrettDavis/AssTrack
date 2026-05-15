using AssTrack.Domain.Models;

namespace AssTrack.Api.Services;

public interface IWebhookNotificationService
{
    Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default);
    Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default);
    Task NotifyIntegrationEventAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
    Task ExecuteRetryAsync(WebhookRetryJob job, CancellationToken cancellationToken = default);
}

