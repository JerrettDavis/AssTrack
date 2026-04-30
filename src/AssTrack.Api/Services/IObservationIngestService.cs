using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;

namespace AssTrack.Api.Services;

public interface IObservationIngestService
{
    Task<IngestResult> IngestAsync(CreateObservationRequest request, CancellationToken cancellationToken = default);
}

public record IngestResult(
    Observation? Created,
    bool IsDuplicate,
    SpeedAlert? SpeedAlert,
    IReadOnlyList<GeofenceBreach> GeofenceBreaches);

/// <summary>
/// Thrown by ObservationIngestService when input validation or device lookup fails.
/// </summary>
public sealed class ObservationIngestException(Dictionary<string, string[]> errors) : Exception("Ingest validation failed.")
{
    public Dictionary<string, string[]> ValidationErrors { get; } = errors;
}
