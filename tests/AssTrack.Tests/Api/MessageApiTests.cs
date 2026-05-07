using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AssTrack.Tests.Api;

public class MessageApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _operatorClient = factory.CreateAuthenticatedClient();
    private readonly HttpClient _ingestClient = factory.CreateIngestClient();

    [Fact]
    public async Task SendMessage_QueuesOutboundMessage_ForBridgeFeed()
    {
        var feedId = await CreateFeedAsync();

        var threadResponse = await _operatorClient.PostAsJsonAsync("/api/messages/threads", new
        {
            channel = "direct",
            provider = "meshtastic",
            integrationFeedId = feedId,
            externalPeerId = "!12f4fb74",
            displayName = "DT01"
        });
        threadResponse.EnsureSuccessStatusCode();
        var thread = await threadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var threadId = thread.GetProperty("id").GetGuid();

        var sendResponse = await _operatorClient.PostAsJsonAsync($"/api/messages/threads/{threadId}/messages", new
        {
            body = "status?"
        });

        Assert.Equal(HttpStatusCode.Accepted, sendResponse.StatusCode);
        var sent = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = sent.GetProperty("id").GetGuid();
        Assert.Equal("queued", sent.GetProperty("status").GetString());

        var queue = await _ingestClient.GetFromJsonAsync<List<JsonElement>>($"/api/integrations/{feedId}/messages/outbound");

        Assert.NotNull(queue);
        Assert.Contains(queue, item =>
            item.GetProperty("id").GetGuid() == messageId &&
            item.GetProperty("externalPeerId").GetString() == "!12f4fb74" &&
            item.GetProperty("body").GetString() == "status?");
    }

    [Fact]
    public async Task InboundMessage_CreatesThread_AndStoresMessage()
    {
        var feedId = await CreateFeedAsync("Meshtastic inbound");

        var inboundResponse = await _ingestClient.PostAsJsonAsync("/api/messages/inbound", new
        {
            channel = "direct",
            provider = "meshtastic",
            integrationFeedId = feedId,
            externalPeerId = "!12f4fb75",
            displayName = "DT01",
            sender = "!12f4fb75",
            body = "arrived at checkpoint",
            providerMessageId = "mesh-001",
            receivedAt = DateTime.UtcNow
        });
        inboundResponse.EnsureSuccessStatusCode();
        var inbound = await inboundResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("received", inbound.GetProperty("status").GetString());

        var threads = await _operatorClient.GetFromJsonAsync<List<JsonElement>>("/api/messages/threads");

        Assert.NotNull(threads);
        var thread = Assert.Single(threads, item => item.GetProperty("externalPeerId").GetString() == "!12f4fb75");
        Assert.Equal("DT01", thread.GetProperty("displayName").GetString());
        Assert.Equal("arrived at checkpoint", thread.GetProperty("lastMessage").GetProperty("body").GetString());
    }

    [Fact]
    public async Task BridgeCan_UpdateQueuedMessageStatus()
    {
        var feedId = await CreateFeedAsync("Meshtastic status");
        var threadResponse = await _operatorClient.PostAsJsonAsync("/api/messages/threads", new
        {
            channel = "direct",
            provider = "meshtastic",
            integrationFeedId = feedId,
            externalPeerId = "!da53e290"
        });
        threadResponse.EnsureSuccessStatusCode();
        var thread = await threadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var threadId = thread.GetProperty("id").GetGuid();

        var sendResponse = await _operatorClient.PostAsJsonAsync($"/api/messages/threads/{threadId}/messages", new
        {
            body = "ping"
        });
        sendResponse.EnsureSuccessStatusCode();
        var queued = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = queued.GetProperty("id").GetGuid();

        var statusResponse = await _ingestClient.PostAsJsonAsync($"/api/messages/{messageId}/status", new
        {
            status = "sent",
            providerMessageId = "mesh-out-1",
            sentAt = DateTime.UtcNow
        });

        statusResponse.EnsureSuccessStatusCode();
        var updated = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", updated.GetProperty("status").GetString());
        Assert.Equal("mesh-out-1", updated.GetProperty("providerMessageId").GetString());
    }

    private async Task<Guid> CreateFeedAsync(string name = "Meshtastic messages")
    {
        var feedResponse = await _operatorClient.PostAsJsonAsync("/api/integrations", new
        {
            name,
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "mesh"
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        return feed.GetProperty("id").GetGuid();
    }
}
