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

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        if (!string.IsNullOrEmpty(_apiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        return client;
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
    public async Task Startup_AllowsEmptyCorsOrigins_ForIntegratedProductionHost()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: []);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
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

    [Fact]
    public async Task Response_IncludesXCorrelationId_Header()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"]);
        var client = factory.CreateAuthenticatedClient();
        
        var response = await client.GetAsync("/api/health");
        response.Headers.Should().ContainKey("X-Correlation-Id");
        response.Headers.GetValues("X-Correlation-Id").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Response_EchoesIncomingXCorrelationId_Header()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"]);
        var client = factory.CreateAuthenticatedClient();
        
        var testCorrelationId = "test-correlation-12345";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Correlation-Id", testCorrelationId);
        
        var response = await client.SendAsync(request);
        response.Headers.Should().ContainKey("X-Correlation-Id");
        response.Headers.GetValues("X-Correlation-Id").First().Should().Be(testCorrelationId);
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders_InProduction()
    {
        await using var factory = new ProductionWebApplicationFactory(corsOrigins: ["https://example.com"]);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/health");

        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").First().Should().Be("no-referrer");
        response.Headers.GetValues("Permissions-Policy").First().Should().Contain("geolocation=()");
        response.Headers.GetValues("Content-Security-Policy").First().Should().Contain("frame-ancestors 'none'");
    }
}
