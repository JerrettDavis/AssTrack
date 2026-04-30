using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Repositories;

namespace AssTrack.Api.Endpoints;

public static class SpeedAlertEndpoints
{
    public static RouteGroupBuilder MapSpeedAlertEndpoints(this RouteGroupBuilder group)
    {
        var alerts = group.MapGroup("/speed-alerts");

        alerts.MapGet(string.Empty, async (
            SpeedAlertRepository speedAlertRepository,
            bool? unacknowledged,
            int? limit,
            DateTimeOffset? since,
            Guid? deviceId,
            Guid? assetId,
            string? format,
            CancellationToken cancellationToken) =>
        {
            var sinceUtc = since?.UtcDateTime;
            var items = await speedAlertRepository.GetRecentAsync(
                limit ?? 100, 
                unacknowledged, 
                sinceUtc, 
                deviceId, 
                assetId, 
                cancellationToken);
            
            if (format?.ToLowerInvariant() == "csv")
            {
                // CSV requires at least one filter
                if (!deviceId.HasValue && !assetId.HasValue && !since.HasValue && !unacknowledged.HasValue)
                {
                    var errors = new Dictionary<string, string[]>
                    {
                        ["filters"] = ["CSV export requires at least one filter parameter (deviceId, assetId, since, or unacknowledged)."]
                    };
                    return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                
                var csv = BuildSpeedAlertCsv(items);
                return Results.Content(csv, "text/csv", System.Text.Encoding.UTF8);
            }
            
            return Results.Ok(items.Select(Map));
        }).RequireAuthorization("Operator");

        alerts.MapPost("/{id:guid}/acknowledge", async (Guid id, AcknowledgeSpeedAlertRequest request, SpeedAlertRepository repository, CancellationToken cancellationToken) =>
        {
            var updated = await repository.AcknowledgeAsync(id, DateTime.UtcNow, request.AcknowledgedBy, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        }).RequireAuthorization("Operator");

        alerts.MapPost("/bulk-acknowledge", async (BulkAcknowledgeSpeedAlertsRequest request, SpeedAlertRepository repository, CancellationToken cancellationToken) =>
        {
            var count = await repository.BulkAcknowledgeAsync(request.Ids, DateTime.UtcNow, request.AcknowledgedBy, cancellationToken);
            return Results.Ok(new { count });
        }).RequireAuthorization("Operator");

        return group;
    }

    private static SpeedAlertDto Map(AssTrack.Domain.Models.SpeedAlert alert) => new(
        alert.Id,
        alert.ObservationId,
        alert.DeviceId,
        alert.AssetId,
        alert.ObservedSpeedKmh,
        alert.ThresholdKmh,
        alert.TriggeredAt,
        alert.Device?.Identifier,
        alert.Asset?.Name,
        alert.AcknowledgedAtUtc,
        alert.AcknowledgedBy);

    private static string BuildSpeedAlertCsv(IReadOnlyList<AssTrack.Domain.Models.SpeedAlert> alerts)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,ObservationId,DeviceId,DeviceIdentifier,AssetId,AssetName,ObservedSpeedKmh,ThresholdKmh,TriggeredAt,AcknowledgedAtUtc,AcknowledgedBy");
        
        foreach (var alert in alerts)
        {
            sb.AppendLine($"{alert.Id},{alert.ObservationId},{alert.DeviceId},{CsvEscape(alert.Device?.Identifier)},{alert.AssetId},{CsvEscape(alert.Asset?.Name)},{alert.ObservedSpeedKmh},{alert.ThresholdKmh},{alert.TriggeredAt:O},{alert.AcknowledgedAtUtc?.ToString("O")},{CsvEscape(alert.AcknowledgedBy)}");
        }
        
        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        
        return value;
    }
}

