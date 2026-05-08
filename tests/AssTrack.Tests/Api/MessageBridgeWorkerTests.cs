using System.Net;
using System.Text;
using AssTrack.Domain.Contracts;
using AssTrack.MessageBridge.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssTrack.Tests.Api;

public sealed class MessageBridgeWorkerTests
{
    [Fact]
    public async Task Bridge_gateway_client_uses_handoff_endpoints_with_shared_secret()
    {
        var handler = new RecordingHandler("""[{"id":"22222222-2222-2222-2222-222222222222","threadId":"33333333-3333-3333-3333-333333333333","integrationFeedId":"11111111-1111-1111-1111-111111111111","channel":"direct","provider":"signal","externalPeerId":"+15551234567","displayName":"Field Lead","recipient":"+15551234567","body":"Copy that","metadata":null,"createdAt":"2026-05-08T12:00:00Z"}]""");
        var client = new BridgeGatewayClient(new HttpClient(handler), Options.Create(new BridgeWorkerOptions
        {
            BridgeBaseUrl = new Uri("http://bridge.local"),
            FeedKey = "signal-local",
            SharedSecret = "bridge-secret",
            OutboundTake = 5
        }));

        var outbound = await client.GetOutboundMessagesAsync(CancellationToken.None);
        await client.PostInboundMessageAsync(new InboundProviderMessage("+15551234567", "Gate open", "direct", "Lead", "+15551234567", "in-1", DateTime.UtcNow, null, null, null), CancellationToken.None);
        await client.UpdateMessageStatusAsync(outbound[0].Id, new UpdateMessageStatusRequest("sent", "out-1", DateTime.UtcNow, null), CancellationToken.None);

        outbound.Should().ContainSingle(message => message.Body == "Copy that");
        handler.Requests.Should().Contain(request => request.Method == HttpMethod.Get && request.PathAndQuery == "/bridge/signal-local/messages/outbound?take=5");
        handler.Requests.Should().Contain(request => request.Method == HttpMethod.Post && request.PathAndQuery == "/bridge/signal-local/messages/inbound");
        handler.Requests.Should().Contain(request => request.Method == HttpMethod.Post && request.PathAndQuery!.Contains("/bridge/signal-local/messages/22222222-2222-2222-2222-222222222222/status"));
        handler.Requests.Should().AllSatisfy(request => request.Secret.Should().Be("bridge-secret"));
    }

    [Fact]
    public async Task Bridge_message_pump_forwards_inbound_and_marks_outbound_sent()
    {
        var gateway = new FakeGatewayHandler();
        var provider = new FakeProvider();
        var pump = new BridgeMessagePump(
            new BridgeGatewayClient(new HttpClient(gateway), Options.Create(new BridgeWorkerOptions
            {
                BridgeBaseUrl = new Uri("http://bridge.local"),
                FeedKey = "telegram-local",
                SharedSecret = "bridge-secret"
            })),
            provider,
            Options.Create(new BridgeWorkerOptions { PollSeconds = 1 }),
            NullLogger<BridgeMessagePump>.Instance);

        await pump.ExecuteOnceAsync(CancellationToken.None);

        provider.SentMessages.Should().ContainSingle(message => message.Body == "Reply from dispatch");
        gateway.InboundBodies.Should().ContainSingle(body => body.Contains("Field update"));
        gateway.StatusBodies.Should().ContainSingle(body => body.Contains("telegram-out-1") && body.Contains("sent"));
    }

    private sealed class FakeProvider : IMessageProviderClient
    {
        public List<OutboundMessageDto> SentMessages { get; } = [];

        public Task<IReadOnlyList<InboundProviderMessage>> ReceiveInboundMessagesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<InboundProviderMessage>>(
            [
                new InboundProviderMessage("12345", "Field update", "direct", "Field Lead", "12345", "telegram-in-1", DateTime.UtcNow, null, null, null)
            ]);

        public Task<ProviderSendResult> SendOutboundMessageAsync(OutboundMessageDto message, CancellationToken cancellationToken)
        {
            SentMessages.Add(message);
            return Task.FromResult(new ProviderSendResult("sent", "telegram-out-1", DateTime.UtcNow, null));
        }
    }

    private sealed class FakeGatewayHandler : HttpMessageHandler
    {
        public List<string> InboundBodies { get; } = [];
        public List<string> StatusBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/messages/inbound", StringComparison.Ordinal))
            {
                InboundBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json(HttpStatusCode.Accepted, "{}");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/status", StringComparison.Ordinal))
            {
                StatusBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json(HttpStatusCode.Accepted, "{}");
            }

            return Json(HttpStatusCode.OK, """[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","threadId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","integrationFeedId":"cccccccc-cccc-cccc-cccc-cccccccccccc","channel":"direct","provider":"telegram","externalPeerId":"12345","displayName":"Field Lead","recipient":"12345","body":"Reply from dispatch","metadata":null,"createdAt":"2026-05-08T12:00:00Z"}]""");
        }
    }

    private sealed class RecordingHandler(string outboundBody) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.TryGetValues("X-Bridge-Secret", out var values) ? values.SingleOrDefault() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return Json(request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted, request.Method == HttpMethod.Get ? outboundBody : "{}");
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string? PathAndQuery, string? Secret, string? Body);

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
