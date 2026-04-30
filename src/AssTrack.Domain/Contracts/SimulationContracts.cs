namespace AssTrack.Domain.Contracts;

public enum SimulationPreset { NormalRoute, SpeedViolation, GeofenceEntryExit }

public record SimulateRequest(SimulationPreset Preset, string? DeviceIdentifier = null);

public record SimulateResult(
    int ObservationsCreated,
    int SpeedAlertsTriggered,
    int GeofenceBreaches,
    Guid DeviceId,
    string DeviceIdentifier,
    Guid? AssetId,
    IReadOnlyList<string> EventLog);
