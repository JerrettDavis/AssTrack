using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AssTrack.Api.Endpoints;

public static class MessageEndpoints
{
    public static RouteGroupBuilder MapMessageEndpoints(this RouteGroupBuilder group)
    {
        var messages = group.MapGroup("/messages");

        messages.MapGet("/threads", async (MessageRepository repository, CancellationToken cancellationToken) =>
        {
            var threads = await repository.GetThreadsAsync(cancellationToken);
            return Results.Ok(threads.Select(MapThread));
        });

        messages.MapPost("/threads", async (
            [FromBody] CreateMessageThreadRequest request,
            MessageRepository repository,
            IntegrationFeedRepository integrationFeeds,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateThreadRequest(request.Channel, request.Provider, request.IntegrationFeedId, request.ExternalPeerId);
            if (validation is not null) return validation;

            if (request.IntegrationFeedId.HasValue &&
                await integrationFeeds.GetByIdAsync(request.IntegrationFeedId.Value, cancellationToken) is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["integrationFeedId"] = ["Integration feed was not found."] });
            }

            var now = DateTime.UtcNow;
            var thread = await repository.CreateThreadAsync(new MessageThread
            {
                Channel = request.Channel.Trim(),
                Provider = request.Provider.Trim(),
                IntegrationFeedId = request.IntegrationFeedId,
                DeviceId = request.DeviceId,
                AssetId = request.AssetId,
                ExternalPeerId = NormalizeNullable(request.ExternalPeerId),
                DisplayName = NormalizeNullable(request.DisplayName) ?? NormalizeNullable(request.ExternalPeerId),
                Subject = NormalizeNullable(request.Subject),
                Metadata = NormalizeNullable(request.Metadata),
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);

            return Results.Created($"/api/messages/threads/{thread.Id}", MapThread(thread));
        });

        messages.MapGet("/threads/{id:guid}", async (Guid id, MessageRepository repository, CancellationToken cancellationToken) =>
        {
            var thread = await repository.GetThreadAsync(id, cancellationToken);
            return thread is null ? Results.NotFound() : Results.Ok(MapThread(thread));
        });

        messages.MapGet("/threads/{id:guid}/messages", async (Guid id, MessageRepository repository, CancellationToken cancellationToken) =>
        {
            var thread = await repository.GetThreadAsync(id, cancellationToken);
            if (thread is null) return Results.NotFound();

            var entries = await repository.GetMessagesAsync(id, cancellationToken);
            return Results.Ok(entries.Select(MapEntry));
        });

        messages.MapPost("/threads/{id:guid}/messages", async (
            Guid id,
            [FromBody] SendMessageRequest request,
            MessageRepository repository,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Message body is required."] });
            }

            var thread = await repository.GetThreadAsync(id, cancellationToken);
            if (thread is null) return Results.NotFound();

            var entry = await repository.AddMessageAsync(new MessageEntry
            {
                ThreadId = id,
                Direction = MessageDirection.Outbound,
                Status = MessageStatus.Queued,
                Recipient = NormalizeNullable(request.Recipient) ?? thread.ExternalPeerId,
                Body = request.Body.Trim(),
                Metadata = NormalizeNullable(request.Metadata),
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            var dto = MapEntry(entry);
            broadcaster.Publish(new LiveEvent(LiveEventType.Message, dto));
            return Results.Accepted($"/api/messages/threads/{id}/messages/{entry.Id}", dto);
        });

        messages.MapPost("/inbound", async (
            [FromBody] InboundMessageRequest request,
            MessageRepository repository,
            IntegrationFeedRepository integrationFeeds,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Message body is required."] });
            }

            if (string.IsNullOrWhiteSpace(request.ExternalPeerId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["externalPeerId"] = ["External peer id is required."] });
            }

            if (request.IntegrationFeedId.HasValue)
            {
                var feed = await integrationFeeds.GetByIdAsync(request.IntegrationFeedId.Value, cancellationToken);
                if (feed is null) return Results.NotFound();
                if (!feed.IsEnabled)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["feed"] = ["Integration feed is disabled."] });
                }
            }

            var thread = await repository.GetOrCreateThreadAsync(
                request.Channel,
                request.Provider,
                request.IntegrationFeedId,
                request.DeviceId,
                request.AssetId,
                request.ExternalPeerId,
                request.DisplayName,
                request.Metadata,
                cancellationToken);

            var receivedAt = (request.ReceivedAt ?? DateTime.UtcNow).ToUniversalTime();
            var entry = await repository.AddMessageAsync(new MessageEntry
            {
                ThreadId = thread.Id,
                Direction = MessageDirection.Inbound,
                Status = MessageStatus.Received,
                Sender = NormalizeNullable(request.Sender) ?? request.ExternalPeerId.Trim(),
                Body = request.Body.Trim(),
                ProviderMessageId = NormalizeNullable(request.ProviderMessageId),
                ReceivedAt = receivedAt,
                CreatedAt = receivedAt,
                Metadata = NormalizeNullable(request.Metadata)
            }, cancellationToken);

            var dto = MapEntry(entry);
            broadcaster.Publish(new LiveEvent(LiveEventType.Message, dto));
            return Results.Ok(dto);
        }).RequireAuthorization("Ingest");

        messages.MapPost("/{messageId:guid}/status", async (
            Guid messageId,
            [FromBody] UpdateMessageStatusRequest request,
            MessageRepository repository,
            ILiveEventBroadcaster broadcaster,
            CancellationToken cancellationToken) =>
        {
            if (!IsValidStatus(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status must be queued, sent, delivered, or failed."] });
            }

            var updated = await repository.UpdateStatusAsync(
                messageId,
                request.Status,
                request.ProviderMessageId,
                request.SentAt,
                request.ErrorMessage,
                cancellationToken);

            if (updated is null) return Results.NotFound();

            var dto = MapEntry(updated);
            broadcaster.Publish(new LiveEvent(LiveEventType.Message, dto));
            return Results.Ok(dto);
        }).RequireAuthorization("Ingest");

        group.MapGet("/integrations/{feedId:guid}/messages/outbound", async (
            Guid feedId,
            [FromQuery] int? take,
            IntegrationFeedRepository integrationFeeds,
            MessageRepository repository,
            CancellationToken cancellationToken) =>
        {
            var feed = await integrationFeeds.GetByIdAsync(feedId, cancellationToken);
            if (feed is null) return Results.NotFound();

            var entries = await repository.GetQueuedOutboundAsync(feedId, take is > 0 and <= 200 ? take.Value : 50, cancellationToken);
            return Results.Ok(entries.Select(MapOutbound));
        }).RequireAuthorization("Ingest");

        return group;
    }

    private static IResult? ValidateThreadRequest(string channel, string provider, Guid? integrationFeedId, string? externalPeerId)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(channel)) errors["channel"] = ["Channel is required."];
        if (string.IsNullOrWhiteSpace(provider)) errors["provider"] = ["Provider is required."];
        if (integrationFeedId.HasValue && string.IsNullOrWhiteSpace(externalPeerId))
        {
            errors["externalPeerId"] = ["External peer id is required for integration-backed threads."];
        }
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static bool IsValidStatus(string status)
        => string.Equals(status, MessageStatus.Queued, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, MessageStatus.Sent, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, MessageStatus.Delivered, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, MessageStatus.Failed, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MessageThreadDto MapThread(MessageThread thread)
        => new(
            thread.Id,
            thread.Channel,
            thread.Provider,
            thread.IntegrationFeedId,
            thread.IntegrationFeed?.Name,
            thread.DeviceId,
            thread.Device?.Identifier,
            thread.Device?.Label,
            thread.AssetId,
            thread.Asset?.Name,
            thread.ExternalPeerId,
            thread.DisplayName,
            thread.Subject,
            thread.Status,
            thread.Metadata,
            ApiDateTime.Utc(thread.CreatedAt),
            ApiDateTime.Utc(thread.UpdatedAt),
            ApiDateTime.Utc(thread.LastMessageAt),
            thread.Messages.OrderByDescending(x => x.CreatedAt).Select(MapEntry).FirstOrDefault());

    private static MessageEntryDto MapEntry(MessageEntry entry)
        => new(
            entry.Id,
            entry.ThreadId,
            entry.Direction,
            entry.Status,
            entry.Sender,
            entry.Recipient,
            entry.Body,
            entry.ProviderMessageId,
            ApiDateTime.Utc(entry.SentAt),
            ApiDateTime.Utc(entry.ReceivedAt),
            ApiDateTime.Utc(entry.CreatedAt),
            entry.ErrorMessage,
            entry.Metadata);

    private static OutboundMessageDto MapOutbound(MessageEntry entry)
    {
        var thread = entry.Thread ?? new MessageThread();
        return new OutboundMessageDto(
            entry.Id,
            entry.ThreadId,
            thread.IntegrationFeedId,
            thread.Channel,
            thread.Provider,
            thread.ExternalPeerId,
            thread.DisplayName,
            entry.Recipient,
            entry.Body,
            entry.Metadata,
            ApiDateTime.Utc(entry.CreatedAt));
    }
}
