using AssTrack.Domain.Models;

namespace AssTrack.Domain.Services;

public static class SpeedAlertEvaluator
{
    public const double DefaultThresholdKmh = 120.0;
    public static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);

    public static SpeedAlert? Evaluate(Observation observation, Guid? assetId = null, double thresholdKmh = DefaultThresholdKmh)
    {
        if (observation.SpeedKmh is null || observation.SpeedKmh <= thresholdKmh)
        {
            return null;
        }

        return new SpeedAlert
        {
            ObservationId = observation.Id,
            DeviceId = observation.DeviceId,
            AssetId = assetId,
            ObservedSpeedKmh = observation.SpeedKmh.Value,
            ThresholdKmh = thresholdKmh,
            TriggeredAt = DateTime.UtcNow
        };
    }
}
