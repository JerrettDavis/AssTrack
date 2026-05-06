using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Services;

public sealed class SimulationService(
    IObservationIngestService ingestService,
    DeviceRepository deviceRepository,
    AssetRepository assetRepository,
    GeofenceRepository geofenceRepository,
    IOptions<SimulationOptions> options) : ISimulationService
{
    public async Task<SimulateResult> SimulateAsync(SimulateRequest request, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            throw new SimulationDisabledException();

        var eventLog = new List<string>();
        var timestamp = DateTime.UtcNow;
        var startTime = timestamp.AddMinutes(-10);

        // Resolve or create device/asset
        Device device;
        Asset? asset = null;

        if (!string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            var existing = await deviceRepository.GetByIdentifierAsync(request.DeviceIdentifier.Trim(), cancellationToken);
            if (existing is not null)
            {
                device = existing;
                eventLog.Add($"Using existing device '{device.Identifier}' (id={device.Id}).");
            }
            else
            {
                device = await deviceRepository.AddAsync(new Device { Identifier = request.DeviceIdentifier.Trim() }, cancellationToken);
                eventLog.Add($"Created new device '{device.Identifier}' (id={device.Id}).");
            }
        }
        else
        {
            var identifier = $"sim-{request.Preset}-{startTime:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            asset = await assetRepository.AddAsync(new Asset { Name = $"Sim Asset - {request.Preset}", IsSeeded = true }, cancellationToken);
            device = await deviceRepository.AddAsync(new Device { Identifier = identifier, AssetId = asset.Id, IsSeeded = true }, cancellationToken);
            // Reload with asset navigation property
            device = (await deviceRepository.GetByIdAsync(device.Id, cancellationToken))!;
            eventLog.Add($"Created temporary device '{device.Identifier}' (id={device.Id}) with asset '{asset.Name}'.");
        }

        // Build waypoints based on preset
        var waypoints = request.Preset switch
        {
            SimulationPreset.NormalRoute => BuildNormalRouteWaypoints(),
            SimulationPreset.SpeedViolation => BuildSpeedViolationWaypoints(),
            SimulationPreset.GeofenceEntryExit => BuildGeofenceEntryExitWaypoints(),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Preset))
        };

        // For GeofenceEntryExit, create temporary geofence before ingesting
        Geofence? tempGeofence = null;
        if (request.Preset == SimulationPreset.GeofenceEntryExit)
        {
            tempGeofence = await geofenceRepository.AddAsync(new Geofence
            {
                Name = $"sim-geofence-{startTime:yyyyMMddHHmmss}",
                Description = "Temporary simulation geofence - will be deactivated after simulation.",
                CenterLatitude = 51.5100,
                CenterLongitude = -0.1000,
                RadiusMeters = 1000,
                IsActive = true,
                IsSeeded = true
            }, cancellationToken);
            eventLog.Add($"Created temporary geofence '{tempGeofence.Name}' (id={tempGeofence.Id}) at 51.5100, -0.1000 radius 1000m.");
        }

        int observationsCreated = 0;
        int speedAlertsTriggered = 0;
        int geofenceBreachCount = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            var (lat, lon, speed) = waypoints[i];
            var observedAt = startTime.AddSeconds(i * 30);

            var req = new CreateObservationRequest(
                DeviceId: device.Id,
                ObservedAt: observedAt,
                Latitude: lat,
                Longitude: lon,
                Altitude: null,
                AccuracyMeters: null,
                SpeedKmh: speed,
                HeadingDegrees: null,
                Metadata: null);

            IngestResult result;
            try
            {
                result = await ingestService.IngestAsync(req, cancellationToken);
            }
            catch (ObservationIngestException ex)
            {
                eventLog.Add($"Point {i + 1}: Ingest failed - {string.Join("; ", ex.ValidationErrors.SelectMany(kv => kv.Value))}");
                continue;
            }

            if (!result.IsDuplicate)
                observationsCreated++;

            if (result.SpeedAlert is not null)
            {
                speedAlertsTriggered++;
                eventLog.Add($"Point {i + 1}: Speed alert triggered at {speed} km/h (threshold {result.SpeedAlert.ThresholdKmh} km/h).");
            }

            if (result.GeofenceBreaches.Count > 0)
            {
                geofenceBreachCount += result.GeofenceBreaches.Count;
                foreach (var breach in result.GeofenceBreaches)
                    eventLog.Add($"Point {i + 1}: Geofence {breach.EventType} event for geofence {breach.GeofenceId}.");
            }

            if (request.Preset == SimulationPreset.SpeedViolation && speed > 120 && result.SpeedAlert is null)
                eventLog.Add($"Point {i + 1}: Speed {speed} km/h exceeded threshold but alert suppressed by cooldown (cooldown = {Domain.Services.SpeedAlertEvaluator.AlertCooldown.TotalMinutes} min, points 30s apart).");
        }

        // Clean up temporary geofence for GeofenceEntryExit
        if (tempGeofence is not null)
        {
            await geofenceRepository.UpdateAsync(
                tempGeofence.Id,
                tempGeofence.Name,
                tempGeofence.Description,
                tempGeofence.ShapeType,
                tempGeofence.CenterLatitude,
                tempGeofence.CenterLongitude,
                tempGeofence.RadiusMeters,
                tempGeofence.PolygonJson,
                false,
                cancellationToken);
            eventLog.Add($"Deactivated temporary geofence '{tempGeofence.Name}' for test isolation.");
        }

        return new SimulateResult(
            ObservationsCreated: observationsCreated,
            SpeedAlertsTriggered: speedAlertsTriggered,
            GeofenceBreaches: geofenceBreachCount,
            DeviceId: device.Id,
            DeviceIdentifier: device.Identifier,
            AssetId: asset?.Id ?? device.AssetId,
            EventLog: eventLog);
    }

    // NormalRoute: 10 points in central London, speeds 30-80 km/h, no speed violations
    private static List<(double Lat, double Lon, double Speed)> BuildNormalRouteWaypoints() =>
    [
        (51.5074, -0.1278, 45),
        (51.5090, -0.1250, 50),
        (51.5110, -0.1200, 55),
        (51.5130, -0.1150, 60),
        (51.5150, -0.1100, 65),
        (51.5170, -0.1060, 70),
        (51.5190, -0.1020, 75),
        (51.5200, -0.0980, 80),
        (51.5190, -0.0940, 55),
        (51.5180, -0.0900, 40)
    ];

    // SpeedViolation: 8 points, first violation fires, subsequent within 5-min cooldown are suppressed
    private static List<(double Lat, double Lon, double Speed)> BuildSpeedViolationWaypoints() =>
    [
        (51.5074, -0.1278, 50),
        (51.5090, -0.1250, 50),
        (51.5110, -0.1200, 50),
        (51.5130, -0.1150, 140),   // triggers alert
        (51.5150, -0.1100, 155),   // within cooldown - suppressed
        (51.5170, -0.1060, 160),   // within cooldown - suppressed
        (51.5190, -0.1020, 60),
        (51.5200, -0.0980, 60)
    ];

    // GeofenceEntryExit: 10 points around geofence center 51.5100, -0.1000, radius 1000m
    // Points 1-3 outside, point 4 enters (~609m), points 4-7 inside, point 8 exits (~1092m), points 8-10 outside
    private static List<(double Lat, double Lon, double Speed)> BuildGeofenceEntryExitWaypoints() =>
    [
        (51.4980, -0.1150, 40),   // outside (~1693m)
        (51.5000, -0.1130, 45),   // outside (~1432m)
        (51.5030, -0.1100, 50),   // outside (~1043m)
        (51.5060, -0.1060, 55),   // inside  (~609m)  - ENTER
        (51.5080, -0.1020, 55),   // inside  (~262m)
        (51.5100, -0.1000, 50),   // inside  (center)
        (51.5110, -0.0960, 45),   // inside  (~299m)
        (51.5130, -0.0850, 50),   // outside (~1092m) - EXIT
        (51.5160, -0.0820, 55),   // outside (~1415m)
        (51.5200, -0.0800, 50)    // outside (~1778m)
    ];
}

public sealed class SimulationDisabledException() : Exception("Simulation is disabled in this environment.");
