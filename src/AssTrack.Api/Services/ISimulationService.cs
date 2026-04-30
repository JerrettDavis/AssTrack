using AssTrack.Domain.Contracts;

namespace AssTrack.Api.Services;

public interface ISimulationService
{
    Task<SimulateResult> SimulateAsync(SimulateRequest request, CancellationToken cancellationToken = default);
}
