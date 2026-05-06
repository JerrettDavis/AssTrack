using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AssTrack.Api.Endpoints;

public static class IntegrationEndpoints
{
    public static RouteGroupBuilder MapIntegrationEndpoints(this RouteGroupBuilder group)
    {
        var integrations = group.MapGroup("/integrations");

        integrations.MapGet("/providers", () => Results.Ok(IntegrationProviderCatalog.GetAll()))
            .RequireAuthorization("Operator");

        integrations.MapGet(string.Empty, async (IntegrationFeedRepository repository, CancellationToken cancellationToken) =>
        {
            var feeds = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(feeds.Select(Map));
        }).RequireAuthorization("Operator");

        integrations.MapGet("/bridge-config", async (IntegrationFeedRepository repository, CancellationToken cancellationToken) =>
        {
            var feeds = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(feeds.Select(feed => new BridgeIntegrationFeedConfigDto(
                feed.Id,
                feed.Name,
                feed.Provider,
                feed.IsEnabled,
                feed.AutoCreateDevices,
                feed.DefaultTags,
                feed.ConfigurationJson,
                feed.UpdatedAt)));
        }).RequireAuthorization("Operator");

        integrations.MapPost(string.Empty, async ([FromBody] CreateIntegrationFeedRequest request, IntegrationFeedRepository repository, CancellationToken cancellationToken) =>
        {
            var validation = ValidateFeed(request.Name, request.Provider);
            if (validation is not null) return validation;

            var provider = IntegrationProviderCatalog.Get(request.Provider.Trim());
            if (provider is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["Provider is not supported."] });
            }

            var feed = new IntegrationFeed
            {
                Name = request.Name.Trim(),
                Provider = provider.Id,
                IsEnabled = request.IsEnabled,
                AutoCreateDevices = request.AutoCreateDevices,
                DefaultTags = NormalizeNullable(request.DefaultTags),
                ConfigurationJson = NormalizeNullable(request.ConfigurationJson),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await repository.AddAsync(feed, cancellationToken);
            return Results.Created($"/api/integrations/{created.Id}", Map(created));
        }).RequireAuthorization("Operator");

        integrations.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateIntegrationFeedRequest request, IntegrationFeedRepository repository, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
            }

            var updated = await repository.UpdateAsync(
                id,
                request.Name.Trim(),
                request.IsEnabled,
                request.AutoCreateDevices,
                request.DefaultTags,
                request.ConfigurationJson,
                cancellationToken);

            return updated is null ? Results.NotFound() : Results.Ok(Map(updated));
        }).RequireAuthorization("Operator");

        integrations.MapDelete("/{id:guid}", async (Guid id, IntegrationFeedRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Operator");

        integrations.MapPost("/{id:guid}/observations", async (
            Guid id,
            [FromBody] IntegrationFeedObservationRequest request,
            IntegrationFeedRepository integrationRepository,
            DeviceRepository deviceRepository,
            IObservationIngestService ingestService,
            CancellationToken cancellationToken) =>
        {
            var feed = await integrationRepository.GetByIdAsync(id, cancellationToken);
            if (feed is null) return Results.NotFound();
            if (!feed.IsEnabled)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["feed"] = ["Integration feed is disabled."] });
            }

            if (string.IsNullOrWhiteSpace(request.ExternalTrackerId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["externalTrackerId"] = ["External tracker id is required."] });
            }

            var externalId = request.ExternalTrackerId.Trim();
            var device = await deviceRepository.GetByIntegrationExternalIdAsync(feed.Id, externalId, cancellationToken);
            var createdDevice = false;

            if (device is null)
            {
                if (!feed.AutoCreateDevices)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["externalTrackerId"] = ["No device is mapped to this external tracker id, and auto-create is disabled."] });
                }

                var identifier = $"{feed.Provider}:{externalId}";
                device = await deviceRepository.GetByIdentifierAsync(identifier, cancellationToken);
                if (device is null)
                {
                    device = await deviceRepository.AddAsync(new Device
                    {
                        Identifier = identifier,
                        Label = NormalizeNullable(request.Label) ?? externalId,
                        Protocol = feed.Provider,
                        Provider = feed.Provider,
                        ExternalId = externalId,
                        Tags = MergeTags(feed.DefaultTags, request.Tags),
                        IntegrationFeedId = feed.Id,
                        AssetId = request.AssetId,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                    createdDevice = true;
                }
                else if (device.IntegrationFeedId is null || string.IsNullOrWhiteSpace(device.ExternalId))
                {
                    device = await deviceRepository.UpdateAsync(
                        device.Id,
                        device.Identifier,
                        NormalizeNullable(request.Label) ?? device.Label,
                        feed.Provider,
                        request.AssetId ?? device.AssetId,
                        feed.Provider,
                        externalId,
                        MergeTags(device.Tags ?? feed.DefaultTags, request.Tags),
                        feed.Id,
                        cancellationToken) ?? device;
                }
            }

            var metadata = BuildMetadata(feed, request);
            var ingest = await ingestService.IngestAsync(new CreateObservationRequest(
                device.Id,
                request.ObservedAt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ObservedAt, DateTimeKind.Utc) : request.ObservedAt.ToUniversalTime(),
                request.Latitude,
                request.Longitude,
                request.Altitude,
                request.AccuracyMeters,
                request.SpeedKmh,
                request.HeadingDegrees,
                metadata,
                device.Identifier), cancellationToken);

            return Results.Ok(new IntegrationIngestResultDto(feed.Id, device.Id, device.Identifier, createdDevice, ObservationEndpoints.Map(ingest.Created!)));
        }).RequireAuthorization("Ingest");

        return group;
    }

    private static IntegrationFeedDto Map(IntegrationFeed feed)
    {
        var provider = IntegrationProviderCatalog.Get(feed.Provider);
        return new IntegrationFeedDto(
            feed.Id,
            feed.Name,
            feed.Provider,
            provider?.Name ?? feed.Provider,
            feed.IsEnabled,
            feed.AutoCreateDevices,
            feed.DefaultTags,
            feed.ConfigurationJson,
            feed.CreatedAt,
            feed.UpdatedAt,
            feed.Devices.Count);
    }

    private static IResult? ValidateFeed(string name, string provider)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        if (string.IsNullOrWhiteSpace(provider)) errors["provider"] = ["Provider is required."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? MergeTags(string? feedTags, string? requestTags)
    {
        var values = new[] { feedTags, requestTags }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string BuildMetadata(IntegrationFeed feed, IntegrationFeedObservationRequest request)
    {
        var metadataParts = new Dictionary<string, object?>
        {
            ["integrationFeedId"] = feed.Id,
            ["provider"] = feed.Provider,
            ["externalTrackerId"] = request.ExternalTrackerId,
            ["sourceMetadata"] = request.Metadata
        };
        return System.Text.Json.JsonSerializer.Serialize(metadataParts);
    }
}
