namespace AssTrack.Domain.Contracts;

public record SeedRequest(bool Reset = false);

public record SeedResult(bool AlreadySeeded, bool ResetPerformed, int AssetsCreated, int DevicesCreated, int GeofencesCreated);
