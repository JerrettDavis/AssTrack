using AssTrack.Domain.Contracts;

namespace AssTrack.Api.Services;

public interface ISeedService
{
    Task<SeedResult> SeedAsync(bool reset, CancellationToken cancellationToken = default);
}

public sealed class SeedingDisabledException() : Exception("Demo seeding is disabled in this environment.");
