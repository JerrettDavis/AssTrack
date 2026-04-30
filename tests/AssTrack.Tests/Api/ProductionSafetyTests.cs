using System.Net;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssTrack.Tests.Api;

/// <summary>
/// A factory that runs the application in Production environment.
/// Supply corsOrigins to override the CORS configuration.
/// Supply apiKey to set the API key (optional).
/// </summary>
internal sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string[] _corsOrigins;
    private readonly string? _apiKey;
    private SqliteConnection? _connection;
    private readonly string _databaseName = $"AssTrackProdTests-{Guid.NewGuid():N}";

    public ProductionWebApplicationFactory(string[] corsOrigins, string? apiKey = "prod-test-api-key")
    {
        _corsOrigins = corsOrigins;
        _apiKey = apiKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        var configValues = new Dictionary<string, string?>();
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            configValues["Auth:ApiKey"] = _apiKey;
        }

        // Add CORS origins indexed style
        for (var i = 0; i < _corsOrigins.Length; i++)
        {
            configValues[$"Cors:AllowedOrigins:{i}"] = _corsOrigins[i];
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(configValues);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AssTrackDbContext>>();
            services.RemoveAll<AssTrackDbContext>();

            _connection?.Dispose();
            _connection = new SqliteConnection($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
            _connection.Open();

            services.AddDbContext<AssTrackDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

public class ProductionSafetyTests
{
    [Fact]
    public async Task Swagger_IsNotAccessible_InProduction()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"]);
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/index.html");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenCorsOriginsEmpty_InProduction()
    {
        using var factory = new ProductionWebApplicationFactory(corsOrigins: []);
        var act = () => factory.CreateClient();
        act.Should().Throw<Exception>()
            .Which.ToString().Should().Contain("Cors:AllowedOrigins must be configured in Production");
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenApiKeyMissing_InProduction()
    {
        using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"], apiKey: null);
        var act = () => factory.CreateClient();
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Auth:ApiKey is not configured");
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenApiKeyEmpty_InProduction()
    {
        using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"], apiKey: "");
        var act = () => factory.CreateClient();
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Auth:ApiKey is not configured");
    }
}

public class SseTokenEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SseTokenEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostSseToken_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSseToken_WithInvalidApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSseToken_WithValidApiKey_Returns200WithToken()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/events/token", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

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
        Assert.Equal(System.Net.HttpStatusCode.OK, tokenResponse.StatusCode);

        var json = await tokenResponse.Content.ReadAsStringAsync();
        // Extract token from JSON - simple parsing for test
        var tokenStart = json.IndexOf("\"token\":\"") + 9;
        var tokenEnd = json.IndexOf("\"", tokenStart);
        var token = json.Substring(tokenStart, tokenEnd - tokenStart);

        // Use token to connect to SSE
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.GetAsync($"/api/events?token={token}", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }
}
