using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace AssTrack.MessageBridge.Worker;

public sealed class BridgeGatewayClient(HttpClient httpClient, IOptions<BridgeWorkerOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public async Task<IReadOnlyList<OutboundMessageDto>> GetOutboundMessagesAsync(CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        var take = Math.Clamp(config.OutboundTake, 1, 200);
        using var request = CreateRequest(HttpMethod.Get, config, $"messages/outbound?take={take}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BridgeGatewayException("Outbound queue fetch failed.", (int)response.StatusCode, body, IsRetryable(response.StatusCode));
        }

        return JsonSerializer.Deserialize<List<OutboundMessageDto>>(body, JsonOptions) ?? [];
    }

    public async Task PostInboundMessageAsync(InboundProviderMessage message, CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, config, "messages/inbound");
        request.Content = JsonContent.Create(new BridgeInboundMessageRequest(
            message.ExternalPeerId,
            message.Body,
            string.IsNullOrWhiteSpace(message.Channel) ? config.DefaultChannel : message.Channel,
            message.DisplayName,
            message.Sender,
            message.ProviderMessageId,
            NormalizeUtc(message.ReceivedAt),
            message.DeviceId,
            message.AssetId,
            message.Metadata));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BridgeGatewayException("Inbound message handoff failed.", (int)response.StatusCode, body, IsRetryable(response.StatusCode));
        }
    }

    public async Task UpdateMessageStatusAsync(Guid messageId, UpdateMessageStatusRequest status, CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, config, $"messages/{messageId}/status");
        request.Content = JsonContent.Create(status with { SentAt = NormalizeUtc(status.SentAt) });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BridgeGatewayException("Message status update failed.", (int)response.StatusCode, body, IsRetryable(response.StatusCode));
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, BridgeWorkerOptions config, string relativePath)
    {
        var path = $"/bridge/{Uri.EscapeDataString(config.FeedKey)}/{relativePath}";
        var request = new HttpRequestMessage(method, new Uri(config.BridgeBaseUrl!, path));
        if (!string.IsNullOrWhiteSpace(config.SharedSecret))
        {
            request.Headers.TryAddWithoutValidation("X-Bridge-Secret", config.SharedSecret);
        }

        return request;
    }

    private BridgeWorkerOptions EnsureConfigured()
    {
        var config = options.Value;
        if (config.BridgeBaseUrl is null)
        {
            throw new InvalidOperationException("BridgeWorker:BridgeBaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(config.FeedKey))
        {
            throw new InvalidOperationException("BridgeWorker:FeedKey is required.");
        }

        return config;
    }

    private static DateTime? NormalizeUtc(DateTime? value)
        => value is null
            ? null
            : value.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value.Value.ToUniversalTime();

    private static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}

public sealed class BridgeGatewayException(string message, int statusCode, string? responseBody, bool retryable) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
    public bool Retryable { get; } = retryable;
}

internal sealed record BridgeInboundMessageRequest(
    string ExternalPeerId,
    string Body,
    string? Channel,
    string? DisplayName,
    string? Sender,
    string? ProviderMessageId,
    DateTime? ReceivedAt,
    Guid? DeviceId,
    Guid? AssetId,
    string? Metadata);
