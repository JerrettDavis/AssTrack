using System.Net;
using System.Net.Http.Json;
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
    public async Task GetEvents_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithInvalidToken_Returns401()
    {
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync("/api/events?token=invalid-token", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithValidToken_Returns200WithEventStream()
    {
        // First, issue a token
        var tokenService = _factory.Services.GetRequiredService<ISseTokenService>();
        var (token, _) = tokenService.IssueToken();

        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync($"/api/events?token={token}", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetEvents_WithValidToken_ReceivesPublishedEvent()
    {
        // Issue a token
        var tokenService = _factory.Services.GetRequiredService<ISseTokenService>();
        var (token, _) = tokenService.IssueToken();

        using var client = _factory.CreateClient();

        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var response = await client.GetAsync($"/api/events?token={token}", HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
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

    [Fact]
    public async Task GetEvents_WithExpiredToken_Returns401()
    {
        // Create a token service configured with 0 minute TTL (expires immediately)
        using var factory = new TestWebApplicationFactory(ttlMinutes: 0);
        var tokenService = factory.Services.GetRequiredService<ISseTokenService>();
        var (token, _) = tokenService.IssueToken();

        // Wait a bit to ensure expiration
        await Task.Delay(100);

        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync($"/api/events?token={token}", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

}
