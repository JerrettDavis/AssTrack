using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace AssTrack.Api.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/system/status", (
            IWebHostEnvironment env,
            IConfiguration configuration,
            IOptions<SimulationOptions> simulationOptions,
            IOptions<WebhookOptions> webhookOptions) =>
        {
            var connStr = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("AssTrack")
                ?? "";
            var dbProvider = connStr.Contains("Data Source", StringComparison.OrdinalIgnoreCase) ? "SQLite" : "Unknown";

            var dto = new SystemStatusDto(
                Environment: env.EnvironmentName,
                SimulationEnabled: simulationOptions.Value.Enabled,
                WebhookConfigured: !string.IsNullOrEmpty(webhookOptions.Value.Url),
                ApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:ApiKey"]),
                IngestApiKeyConfigured: !string.IsNullOrEmpty(configuration["Auth:IngestApiKey"]),
                SwaggerEnabled: configuration.GetValue<bool>("Swagger:Enabled"),
                RateLimitPermitLimit: configuration.GetValue<int>("RateLimiting:IngestPermitLimit", 60),
                RateLimitWindowSeconds: configuration.GetValue<int>("RateLimiting:IngestWindowSeconds", 60),
                DatabaseProvider: dbProvider
            );

            return Results.Ok(dto);
        })
        .WithName("GetSystemStatus")
        .WithSummary("Get sanitized system configuration status.")
        .RequireAuthorization("Operator");

        return group;
    }
}

