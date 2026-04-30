using System.Net;
using AssTrack.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssTrack.Tests.Api;

public class SseEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SseEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetEvents_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithValidKeyInHeader_Returns200WithEventStream()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetEvents_WithValidKeyAsQueryParam_Returns200WithEventStream()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync(
            $"/api/events?apiKey={TestWebApplicationFactory.TestApiKey}",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetEvents_ConnectedClient_ReceivesPublishedEvent()
    {
        using var client = _factory.CreateAuthenticatedClient();

        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var broadcaster = _factory.Services.GetRequiredService<ILiveEventBroadcaster>();

        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);

        // Wait for ": connected" heartbeat — confirms subscription is registered and stream is flowing
        using var heartbeatCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!heartbeatCts.IsCancellationRequested)
        {
            string? line;
            try { line = await reader.ReadLineAsync(heartbeatCts.Token); }
            catch (OperationCanceledException) { break; }
            if (line is null) break;
            if (line.StartsWith(":")) break; // got the heartbeat
        }
        Assert.False(heartbeatCts.IsCancellationRequested, "Did not receive ': connected' heartbeat within timeout");

        // Subscription is now active — publish an event
        broadcaster.Publish(new LiveEvent(LiveEventType.Observation, new { id = "test-event" }));

        // Read the SSE event lines
        var lines = new List<string>();
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!readCts.Token.IsCancellationRequested)
        {
            string? line;
            try { line = await reader.ReadLineAsync(readCts.Token); }
            catch (OperationCanceledException) { break; }
            if (line is null) break;
            if (line.Length > 0) lines.Add(line);
            if (lines.Count >= 2) break;
        }

        Assert.Contains(lines, l => l.StartsWith("event: observation"));
        Assert.Contains(lines, l => l.StartsWith("data: "));
    }
}
