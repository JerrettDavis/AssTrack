using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using AssTrack.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssTrack.Api.Endpoints;

public static class SensorEndpoints
{
    public static RouteGroupBuilder MapSensorEndpoints(this RouteGroupBuilder group)
    {
        var sensors = group.MapGroup("/sensors");

        sensors.MapGet("/readings", async (
            SensorReadingRepository repository,
            Guid? assetId,
            Guid? deviceId,
            string? sensorType,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var items = await repository.GetRecentAsync(
                assetId,
                deviceId,
                sensorType,
                from?.UtcDateTime,
                to?.UtcDateTime,
                limit ?? 200,
                cancellationToken);
            return Results.Ok(items.Select(Map));
        }).RequireAuthorization("Operator");

        sensors.MapPost("/readings", async (
            [FromBody] CreateSensorReadingRequest request,
            SensorReadingRepository sensorRepository,
            AssetRepository assetRepository,
            DeviceRepository deviceRepository,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            var validation = await ValidateAsync(request, assetRepository, deviceRepository, cancellationToken);
            if (validation.Errors.Count > 0)
            {
                return Results.ValidationProblem(validation.Errors);
            }

            var reading = await sensorRepository.AddAsync(new SensorReading
            {
                AssetId = validation.AssetId,
                DeviceId = validation.DeviceId,
                IntegrationFeedId = request.IntegrationFeedId,
                SensorType = NormalizeSensorType(request.SensorType),
                Name = NormalizeNullable(request.Name),
                NumericValue = request.NumericValue,
                TextValue = NormalizeNullable(request.TextValue),
                Unit = NormalizeNullable(request.Unit),
                ObservedAt = NormalizeObservedAt(request.ObservedAt),
                ReceivedAt = DateTime.UtcNow,
                Metadata = NormalizeNullable(request.Metadata)
            }, cancellationToken);

            broadcaster.PublishDataChanged("sensor_reading", "created", reading.Id, new { reading.AssetId, reading.DeviceId });
            return Results.Created($"/api/sensors/readings/{reading.Id}", Map(reading));
        }).RequireRateLimiting("ingest").RequireAuthorization("Ingest");

        return group;
    }

    internal static SensorReadingDto Map(SensorReading reading) => new(
        reading.Id,
        reading.AssetId,
        reading.Asset?.Name,
        reading.DeviceId,
        reading.Device?.Identifier,
        reading.IntegrationFeedId,
        reading.SensorType,
        reading.Name,
        reading.NumericValue,
        reading.TextValue,
        reading.Unit,
        ApiDateTime.Utc(reading.ObservedAt),
        ApiDateTime.Utc(reading.ReceivedAt),
        reading.Metadata);

    private static async Task<(Dictionary<string, string[]> Errors, Guid? AssetId, Guid? DeviceId)> ValidateAsync(
        CreateSensorReadingRequest request,
        AssetRepository assetRepository,
        DeviceRepository deviceRepository,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.SensorType))
        {
            errors["sensorType"] = ["Sensor type is required."];
        }

        if (request.NumericValue is null && string.IsNullOrWhiteSpace(request.TextValue))
        {
            errors["value"] = ["Either numericValue or textValue is required."];
        }

        Guid? deviceId = request.DeviceId;
        if (!deviceId.HasValue && !string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            var device = await deviceRepository.GetByIdentifierAsync(request.DeviceIdentifier.Trim(), cancellationToken);
            if (device is null)
            {
                errors["deviceIdentifier"] = ["Device identifier was not found."];
            }
            else
            {
                deviceId = device.Id;
            }
        }

        Guid? assetId = request.AssetId;
        if (!assetId.HasValue && deviceId.HasValue)
        {
            var device = await deviceRepository.GetByIdAsync(deviceId.Value, cancellationToken);
            assetId = device?.AssetId;
        }

        if (request.AssetId.HasValue && await assetRepository.GetByIdAsync(request.AssetId.Value, cancellationToken) is null)
        {
            errors["assetId"] = ["Asset was not found."];
        }

        if (deviceId.HasValue && await deviceRepository.GetByIdAsync(deviceId.Value, cancellationToken) is null)
        {
            errors["deviceId"] = ["Device was not found."];
        }

        if (!assetId.HasValue && !deviceId.HasValue)
        {
            errors["scope"] = ["Either assetId, deviceId, or deviceIdentifier is required."];
        }

        return (errors, assetId, deviceId);
    }

    private static DateTime NormalizeObservedAt(DateTime? value)
    {
        var observedAt = value ?? DateTime.UtcNow;
        return observedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(observedAt, DateTimeKind.Utc)
            : observedAt.ToUniversalTime();
    }

    private static string NormalizeSensorType(string value)
        => value.Trim().Replace(' ', '_').Replace('-', '_').ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
