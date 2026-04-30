using AssTrack.Api.Services;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssTrack.Tests.Api;

/// <summary>
/// No-op webhook service used in tests that don't need to inspect webhook calls.
/// Prevents any real outbound HTTP requests during test runs.
/// </summary>
file sealed class NullWebhookService : IWebhookNotificationService
{
    public Task NotifySpeedAlertAsync(SpeedAlert alert, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyGeofenceBreachAsync(GeofenceBreach breach, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key";
    public const string TestIngestApiKey = "test-ingest-key";
    private SqliteConnection? _connection;
    private readonly string _databaseName = $"AssTrackTests-{Guid.NewGuid():N}";
    private readonly int? _ttlMinutes;

    public TestWebApplicationFactory() { }

    internal TestWebApplicationFactory(int? ttlMinutes)
    {
        _ttlMinutes = ttlMinutes;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Auth:ApiKey"] = TestApiKey,
                ["Auth:IngestApiKey"] = TestIngestApiKey
            };
            
            if (_ttlMinutes.HasValue)
            {
                configDict["SseToken:TtlMinutes"] = _ttlMinutes.ToString();
            }

            config.AddInMemoryCollection(configDict);
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

            // Replace the real webhook service with a no-op to avoid outbound HTTP in tests.
            services.RemoveAll<IWebhookNotificationService>();
            services.AddSingleton<IWebhookNotificationService, NullWebhookService>();
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
        return client;
    }

    public HttpClient CreateIngestClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestIngestApiKey);
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
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

