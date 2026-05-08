using AssTrack.Domain.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssTrack.MessageBridge.Worker;

public sealed class BridgeMessagePump(
    BridgeGatewayClient gateway,
    IMessageProviderClient provider,
    IOptions<BridgeWorkerOptions> options,
    ILogger<BridgeMessagePump> logger)
{
    public async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var message in await provider.ReceiveInboundMessagesAsync(cancellationToken))
        {
            try
            {
                await gateway.PostInboundMessageAsync(message, cancellationToken);
                logger.LogInformation("Forwarded inbound provider message {ProviderMessageId} from {ExternalPeerId}.", message.ProviderMessageId, message.ExternalPeerId);
            }
            catch (BridgeGatewayException ex)
            {
                logger.LogWarning("Inbound message handoff failed with {StatusCode}: {ResponseBody}", ex.StatusCode, ex.ResponseBody);
            }
        }

        IReadOnlyList<OutboundMessageDto> outbound;
        try
        {
            outbound = await gateway.GetOutboundMessagesAsync(cancellationToken);
        }
        catch (BridgeGatewayException ex)
        {
            logger.LogWarning("Outbound queue fetch failed with {StatusCode}: {ResponseBody}", ex.StatusCode, ex.ResponseBody);
            return;
        }

        foreach (var message in outbound)
        {
            await SendOutboundAsync(message, cancellationToken);
        }
    }

    private async Task SendOutboundAsync(OutboundMessageDto message, CancellationToken cancellationToken)
    {
        ProviderSendResult result;
        try
        {
            result = await provider.SendOutboundMessageAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Provider send failed for message {MessageId}.", message.Id);
            result = new ProviderSendResult("failed", null, DateTime.UtcNow, ex.Message);
        }

        try
        {
            await gateway.UpdateMessageStatusAsync(message.Id, new UpdateMessageStatusRequest(
                result.Status,
                result.ProviderMessageId,
                result.SentAt,
                result.ErrorMessage), cancellationToken);
        }
        catch (BridgeGatewayException ex)
        {
            logger.LogWarning("Status update failed for message {MessageId} with {StatusCode}: {ResponseBody}", message.Id, ex.StatusCode, ex.ResponseBody);
        }
    }

    public TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 1, 300));
}
