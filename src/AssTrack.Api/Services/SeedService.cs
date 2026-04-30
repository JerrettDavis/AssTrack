using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Services;

public sealed class SeedService(
    AssTrackDbContext db,
    IOptions<SimulationOptions> simulationOptions,
    IObservationIngestService ingestService) : ISeedService
{
    public async Task<SeedResult> SeedAsync(bool reset, CancellationToken cancellationToken = default)
    {
        if (!simulationOptions.Value.Enabled)
            throw new SeedingDisabledException();

        if (reset)
        {
            var seededDeviceIds = await db.Devices
                .Where(d => d.IsSeeded)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (seededDeviceIds.Count > 0)
            {
                // SpeedAlert.DeviceId and GeofenceBreach.DeviceId use DeleteBehavior.Restrict,
                // so they must be explicitly removed before the seeded devices can be deleted.
                await db.SpeedAlerts
                    .Where(s => seededDeviceIds.Contains(s.DeviceId))
                    .ExecuteDeleteAsync(cancellationToken);

                await db.GeofenceBreaches
                    .Where(b => seededDeviceIds.Contains(b.DeviceId))
                    .ExecuteDeleteAsync(cancellationToken);

                await db.DeviceGeofenceStates
                    .Where(s => seededDeviceIds.Contains(s.DeviceId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await db.Geofences.Where(g => g.IsSeeded).ExecuteDeleteAsync(cancellationToken);
            await db.Devices.Where(d => d.IsSeeded).ExecuteDeleteAsync(cancellationToken);
            await db.Assets.Where(a => a.IsSeeded).ExecuteDeleteAsync(cancellationToken);
        }

        if (!reset && await db.Assets.AnyAsync(a => a.IsSeeded, cancellationToken))
        {
            return new SeedResult(AlreadySeeded: true, ResetPerformed: false, AssetsCreated: 0, DevicesCreated: 0, GeofencesCreated: 0);
        }

        var now = DateTime.UtcNow;

        var vanAlpha = new Asset { Name = "Fleet Van Alpha", Description = "Demo fleet van", Category = "Van", IsSeeded = true, CreatedAt = now, UpdatedAt = now };
        var vanBeta = new Asset { Name = "Fleet Van Beta", Description = "Demo fleet van", Category = "Van", IsSeeded = true, CreatedAt = now, UpdatedAt = now };
        var depotAlpha = new Asset { Name = "Depot Alpha", Description = "Demo depot asset", Category = "Depot", IsSeeded = true, CreatedAt = now, UpdatedAt = now };

        db.Assets.AddRange(vanAlpha, vanBeta, depotAlpha);
        await db.SaveChangesAsync(cancellationToken);

        var deviceAlpha = new Device { Identifier = "demo-van-alpha", Label = "Fleet Van Alpha", Protocol = "https", AssetId = vanAlpha.Id, IsSeeded = true, CreatedAt = now };
        var deviceBeta = new Device { Identifier = "demo-van-beta", Label = "Fleet Van Beta", Protocol = "https", AssetId = vanBeta.Id, IsSeeded = true, CreatedAt = now };
        var deviceDepot = new Device { Identifier = "demo-depot-alpha", Label = "Depot Alpha", Protocol = "https", AssetId = depotAlpha.Id, IsSeeded = true, CreatedAt = now };

        db.Devices.AddRange(deviceAlpha, deviceBeta, deviceDepot);
        await db.SaveChangesAsync(cancellationToken);

        var londonCentre = new Geofence { Name = "London City Centre", Description = "Demo geofence - London city centre", CenterLatitude = 51.5074, CenterLongitude = -0.1278, RadiusMeters = 1500, IsActive = true, IsSeeded = true, CreatedAt = now };
        var heathrow = new Geofence { Name = "Heathrow", Description = "Demo geofence - Heathrow airport area", CenterLatitude = 51.4700, CenterLongitude = -0.4543, RadiusMeters = 2000, IsActive = true, IsSeeded = true, CreatedAt = now };

        db.Geofences.AddRange(londonCentre, heathrow);
        await db.SaveChangesAsync(cancellationToken);

        var baseTime = now.AddHours(-2);
        var alphaWaypoints = new[]
        {
            (51.5074, -0.1278, 35.0),
            (51.5090, -0.1250, 45.0),
            (51.5110, -0.1200, 52.0),
            (51.5130, -0.1150, 60.0),
            (51.5150, -0.1100, 55.0),
        };
        var betaWaypoints = new[]
        {
            (51.4900, -0.4600, 40.0),
            (51.4920, -0.4550, 50.0),
            (51.4950, -0.4500, 58.0),
            (51.4980, -0.4450, 65.0),
            (51.5000, -0.4400, 70.0),
        };

        for (int i = 0; i < alphaWaypoints.Length; i++)
        {
            var (lat, lon, speed) = alphaWaypoints[i];
            try
            {
                await ingestService.IngestAsync(new CreateObservationRequest(
                    DeviceId: deviceAlpha.Id,
                    ObservedAt: baseTime.AddMinutes(i * 10),
                    Latitude: lat,
                    Longitude: lon,
                    Altitude: null,
                    AccuracyMeters: null,
                    SpeedKmh: speed,
                    HeadingDegrees: null,
                    Metadata: null), cancellationToken);
            }
            catch (ObservationIngestException) { }
        }

        for (int i = 0; i < betaWaypoints.Length; i++)
        {
            var (lat, lon, speed) = betaWaypoints[i];
            try
            {
                await ingestService.IngestAsync(new CreateObservationRequest(
                    DeviceId: deviceBeta.Id,
                    ObservedAt: baseTime.AddMinutes(i * 10),
                    Latitude: lat,
                    Longitude: lon,
                    Altitude: null,
                    AccuracyMeters: null,
                    SpeedKmh: speed,
                    HeadingDegrees: null,
                    Metadata: null), cancellationToken);
            }
            catch (ObservationIngestException) { }
        }

        return new SeedResult(
            AlreadySeeded: false,
            ResetPerformed: reset,
            AssetsCreated: 3,
            DevicesCreated: 3,
            GeofencesCreated: 2);
    }
}
