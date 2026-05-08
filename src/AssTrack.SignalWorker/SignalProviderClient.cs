using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.MessageBridge.Worker;
using Microsoft.Extensions.Options;

namespace AssTrack.SignalWorker;

public sealed class SignalProviderClient(HttpClient httpClient, IOptions<SignalWorkerOptions> options) : IMessageProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public async Task<IReadOnlyList<InboundProviderMessage>> ReceiveInboundMessagesAsync(CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        var timeout = Math.Clamp(config.ReceiveTimeoutSeconds, 0, 30);
        using var response = await httpClient.GetAsync(new Uri(config.SignalBaseUrl!, $"/v1/receive/{Uri.EscapeDataString(config.Account)}?timeout={timeout}"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var items = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
            : document.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array
                ? messages.EnumerateArray()
                : [];

        return items.Select(ParseInbound).Where(message => message is not null).Select(message => message!).ToList();
    }

    public async Task<ProviderSendResult> SendOutboundMessageAsync(OutboundMessageDto message, CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        var recipient = message.Recipient ?? message.ExternalPeerId;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return new ProviderSendResult("failed", null, DateTime.UtcNow, "Signal recipient is required.");
        }

        using var response = await httpClient.PostAsJsonAsync(new Uri(config.SignalBaseUrl!, "/v2/send"), new
        {
            number = config.Account,
            recipients = new[] { recipient },
            message = message.Body
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerId = TryReadProviderMessageId(body);
        return response.IsSuccessStatusCode
            ? new ProviderSendResult("sent", providerId, DateTime.UtcNow, null)
            : new ProviderSendResult("failed", providerId, DateTime.UtcNow, $"Signal API returned {(int)response.StatusCode}: {body}");
    }

    private InboundProviderMessage? ParseInbound(JsonElement item)
    {
        var envelope = item.TryGetProperty("envelope", out var envelopeElement) ? envelopeElement : item;
        var dataMessage = envelope.TryGetProperty("dataMessage", out var dataElement) ? dataElement : envelope;
        var body = ReadString(dataMessage, "message") ?? ReadString(item, "message") ?? ReadString(item, "body");
        var source = ReadString(envelope, "sourceNumber") ?? ReadString(envelope, "source") ?? ReadString(item, "source");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var timestamp = ReadInt64(envelope, "timestamp") ?? ReadInt64(item, "timestamp");
        var receivedAt = timestamp is null ? DateTime.UtcNow : DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).UtcDateTime;
        var groupId = ReadString(dataMessage, "groupId") ?? ReadString(dataMessage, "groupInfo", "groupId");

        return new InboundProviderMessage(
            string.IsNullOrWhiteSpace(groupId) ? source : groupId,
            body,
            string.IsNullOrWhiteSpace(groupId) ? "direct" : "group",
            ReadString(envelope, "sourceName") ?? source,
            source,
            timestamp?.ToString(),
            receivedAt,
            null,
            null,
            JsonSerializer.Serialize(new { source = "signal-cli-rest-api", sourceNumber = source, groupId }, JsonOptions));
    }

    private SignalWorkerOptions EnsureConfigured()
    {
        var config = options.Value;
        if (config.SignalBaseUrl is null)
        {
            throw new InvalidOperationException("SignalWorker:SignalBaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(config.Account))
        {
            throw new InvalidOperationException("SignalWorker:Account is required.");
        }

        return config;
    }

    private static string? TryReadProviderMessageId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadString(document.RootElement, "timestamp") ?? ReadString(document.RootElement, "id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static string? ReadString(JsonElement element, string parent, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(parent, out var parentElement) &&
           parentElement.ValueKind == JsonValueKind.Object
            ? ReadString(parentElement, property)
            : null;

    private static long? ReadInt64(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }
}
