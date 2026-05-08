using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.MessageBridge.Worker;
using Microsoft.Extensions.Options;

namespace AssTrack.TelegramWorker;

public sealed class TelegramProviderClient(HttpClient httpClient, IOptions<TelegramWorkerOptions> options) : IMessageProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;
    private int? _nextOffset;

    public async Task<IReadOnlyList<InboundProviderMessage>> ReceiveInboundMessagesAsync(CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        _nextOffset ??= ReadOffset(config.OffsetFile);

        var limit = Math.Clamp(config.ReceiveLimit, 1, 100);
        var url = BotUri(config, $"getUpdates?timeout=0&limit={limit}" + (_nextOffset is null ? "" : $"&offset={_nextOffset}"));
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var messages = new List<InboundProviderMessage>();
        foreach (var update in result.EnumerateArray())
        {
            if (update.TryGetProperty("update_id", out var updateIdElement) && updateIdElement.TryGetInt32(out var updateId))
            {
                _nextOffset = Math.Max(_nextOffset ?? 0, updateId + 1);
            }

            var message = ParseInbound(update);
            if (message is not null)
            {
                messages.Add(message);
            }
        }

        WriteOffset(config.OffsetFile, _nextOffset);
        return messages;
    }

    public async Task<ProviderSendResult> SendOutboundMessageAsync(OutboundMessageDto message, CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        var chatId = message.Recipient ?? message.ExternalPeerId;
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new ProviderSendResult("failed", null, DateTime.UtcNow, "Telegram chat id is required.");
        }

        using var response = await httpClient.PostAsJsonAsync(BotUri(config, "sendMessage"), new
        {
            chat_id = chatId,
            text = message.Body
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerId = TryReadMessageId(body);
        return response.IsSuccessStatusCode
            ? new ProviderSendResult("sent", providerId, DateTime.UtcNow, null)
            : new ProviderSendResult("failed", providerId, DateTime.UtcNow, $"Telegram API returned {(int)response.StatusCode}: {body}");
    }

    private InboundProviderMessage? ParseInbound(JsonElement update)
    {
        if (!update.TryGetProperty("message", out var message) && !update.TryGetProperty("channel_post", out message))
        {
            return null;
        }

        var body = ReadString(message, "text") ?? ReadString(message, "caption");
        if (string.IsNullOrWhiteSpace(body) ||
            !message.TryGetProperty("chat", out var chat) ||
            !TryReadLong(chat, "id", out var chatId))
        {
            return null;
        }

        message.TryGetProperty("from", out var from);
        var displayName = ReadString(chat, "title") ?? JoinName(ReadString(from, "first_name"), ReadString(from, "last_name")) ?? ReadString(from, "username");
        var sender = TryReadLong(from, "id", out var fromId) ? fromId.ToString() : ReadString(from, "username");
        var messageId = TryReadLong(message, "message_id", out var id) ? id.ToString() : null;
        var receivedAt = TryReadLong(message, "date", out var unixSeconds) ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime : DateTime.UtcNow;

        return new InboundProviderMessage(
            chatId.ToString(),
            body,
            ReadString(chat, "type") == "private" ? "direct" : "group",
            displayName,
            sender,
            messageId,
            receivedAt,
            null,
            null,
            JsonSerializer.Serialize(new { source = "telegram-bot-api", chatId, sender, messageId }, JsonOptions));
    }

    private TelegramWorkerOptions EnsureConfigured()
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            throw new InvalidOperationException("TelegramWorker:BotToken is required.");
        }

        return config;
    }

    private static Uri BotUri(TelegramWorkerOptions config, string method)
        => new(config.ApiBaseUrl, $"/bot{config.BotToken}/{method}");

    private static int? ReadOffset(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return int.TryParse(File.ReadAllText(path), out var value) ? value : null;
    }

    private static void WriteOffset(string? path, int? offset)
    {
        if (string.IsNullOrWhiteSpace(path) || offset is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, offset.Value.ToString());
    }

    private static string? TryReadMessageId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("result", out var result) &&
                   TryReadLong(result, "message_id", out var messageId)
                ? messageId.ToString()
                : null;
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

    private static bool TryReadLong(JsonElement element, string property, out long value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var propertyValue) &&
               propertyValue.ValueKind == JsonValueKind.Number &&
               propertyValue.TryGetInt64(out value);
    }

    private static string? JoinName(string? firstName, string? lastName)
    {
        var joined = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
