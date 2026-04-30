using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AssTrack.Tests.Api;

public class SseTokenEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SseTokenEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostEventsToken_WithoutAuth_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/events/token", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEventsToken_WithValidAuth_Returns200AndToken()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/events/token", new { });
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var body = await response.Content.ReadFromJsonAsync<SseTokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task PostSseToken_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSseToken_WithInvalidApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSseToken_WithValidApiKey_Returns200WithToken()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(json);
        Assert.Contains("token", json);
        Assert.Contains("expiresAt", json);
    }

    [Fact]
    public async Task PostSseToken_ReturnsValidTokenThatWorksImmediately()
    {
        // Get a token
        using var authClient = _factory.CreateAuthenticatedClient();
        var tokenResponse = await authClient.PostAsync("/api/events/token", null);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        // Extract token from JSON - simple parsing for test
        var tokenStart = json.IndexOf("\"token\":\"") + 9;
        var tokenEnd = json.IndexOf("\"", tokenStart);
        var token = json.Substring(tokenStart, tokenEnd - tokenStart);

        // Use token to connect to SSE
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync($"/api/events?token={token}", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostEventsToken_WithIngestKey_Returns403()
    {
        using var client = _factory.CreateIngestClient();
        var response = await client.PostAsJsonAsync("/api/events/token", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record SseTokenResponse(string Token, DateTimeOffset ExpiresAt);
}
