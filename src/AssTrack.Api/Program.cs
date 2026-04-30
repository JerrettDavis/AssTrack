using AssTrack.Api.Auth;
using AssTrack.Api.Endpoints;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration.GetConnectionString("AssTrack") ?? "Data Source=asstrack.db";

builder.Services.AddDbContext<AssTrackDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AssetRepository>();
builder.Services.AddScoped<DeviceRepository>();
builder.Services.AddScoped<ObservationRepository>();
builder.Services.AddScoped<GeofenceRepository>();
builder.Services.AddScoped<SpeedAlertRepository>();
builder.Services.AddScoped<GeofenceBreachRepository>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Operator", policy => policy.RequireRole("operator"));
    options.AddPolicy("Ingest", policy => policy.RequireRole("ingest"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ingest", limiterOptions =>
    {
        limiterOptions.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:IngestPermitLimit");
        limiterOptions.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:IngestWindowSeconds"));
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "API key authentication"
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey"),
            new List<string>()
        }
    });
});
builder.Services.AddHealthChecks().AddDbContextCheck<AssTrackDbContext>("database");
builder.Services.AddProblemDetails();

builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>();
builder.Services.AddScoped<IObservationIngestService, ObservationIngestService>();
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection(SimulationOptions.SectionName));
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddSingleton<ILiveEventBroadcaster, LiveEventBroadcaster>();
builder.Services.AddSingleton<ISseTokenService, SseTokenService>();

var app = builder.Build();

{
    var apiKey = builder.Configuration["Auth:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        var isDevOrTesting = app.Environment.IsDevelopment() ||
            string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        
        if (!isDevOrTesting)
        {
            throw new InvalidOperationException(
                "Auth:ApiKey is not configured. Set Auth__ApiKey environment variable or configure in appsettings. " +
                "See .env.example for details.");
        }
    }
}

if (app.Environment.IsProduction())
{
    var liveCorsOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (liveCorsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must be configured in Production. " +
            "Set at least one origin via appsettings or Cors__AllowedOrigins__0 environment variable.");
    }
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
    dbContext.Database.Migrate();
}

var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();
api.MapAssetEndpoints();
api.MapDeviceEndpoints();
api.MapObservationEndpoints();
api.MapGeofenceEndpoints();
api.MapSpeedAlertEndpoints();
api.MapWebhookEndpoints();
api.MapSystemEndpoints();
api.MapSseTokenEndpoints();
api.MapAuthEndpoints();
api.MapGet("/alerts/summary", async (SpeedAlertRepository speedAlerts, GeofenceBreachRepository breaches, CancellationToken ct) =>
{
    var speedCount = await speedAlerts.GetUnacknowledgedCountAsync(ct);
    var breachCount = await breaches.GetUnacknowledgedCountAsync(ct);
    return Results.Ok(new AlertSummaryDto(speedCount, breachCount));
}).RequireAuthorization("Operator");
api.MapGet("/health", async (AssTrackDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", database = ex.Message }, statusCode: 503);
    }
}).AllowAnonymous();

// SSE endpoint is anonymous but requires a token query parameter
app.MapGet("/api/events", EventsEndpoints.HandleSseAsync).AllowAnonymous();

if (swaggerEnabled)
    app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

static Task WriteHealthCheckResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
        })
    });
    return ctx.Response.WriteAsync(result);
}

public partial class Program;

