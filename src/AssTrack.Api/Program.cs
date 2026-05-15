using AssTrack.Api.Auth;
using AssTrack.Api.Endpoints;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration.GetConnectionString("AssTrack") ?? "Data Source=asstrack.db";

builder.Services.AddDbContext<AssTrackDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AssetRepository>();
builder.Services.AddScoped<DeviceRepository>();
builder.Services.AddScoped<ObservationRepository>();
builder.Services.AddScoped<GeofenceRepository>();
builder.Services.AddScoped<SpeedAlertRepository>();
builder.Services.AddScoped<GeofenceBreachRepository>();
builder.Services.AddScoped<IntegrationFeedRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<AlertRoutingRuleRepository>();
builder.Services.AddScoped<SensorReadingRepository>();
builder.Services.AddScoped<MaintenanceScheduleRepository>();
builder.Services.AddScoped<CustodyRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddScoped<AuditEventRepository>();
builder.Services.AddScoped<IntegrationEventRepository>();
builder.Services.AddScoped<WebhookSubscriptionRepository>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment() ||
            string.Equals(builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
        }
    });
});

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AssTrackPolicies.Viewer, policy => policy.RequireRole(AssTrackRoles.Viewer, AssTrackRoles.Operator, AssTrackRoles.Admin));
    options.AddPolicy(AssTrackPolicies.Operator, policy => policy.RequireRole(AssTrackRoles.Operator, AssTrackRoles.Admin));
    options.AddPolicy(AssTrackPolicies.Admin, policy => policy.RequireRole(AssTrackRoles.Admin));
    options.AddPolicy(AssTrackPolicies.Ingest, policy => policy.RequireRole(AssTrackRoles.Ingest));
    options.AddPolicy(AssTrackPolicies.Enterprise, policy => policy.RequireClaim(AssTrackClaimTypes.AccessTier, AssTrackAccessTiers.Enterprise));
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
var webhookRetryChannel = Channel.CreateBounded<WebhookRetryJob>(
    new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });
builder.Services.AddSingleton(webhookRetryChannel);
builder.Services.AddSingleton(webhookRetryChannel.Reader);
builder.Services.AddSingleton(webhookRetryChannel.Writer);
builder.Services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>();
builder.Services.AddHostedService<WebhookRetryWorker>();
builder.Services.AddScoped<IObservationIngestService, ObservationIngestService>();
builder.Services.AddScoped<IAlertRoutingService, AlertRoutingService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IIntegrationEventService, IntegrationEventService>();
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection(SimulationOptions.SectionName));
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<ISeedService, SeedService>();
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
    dbContext.Database.Migrate();
}

var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
    }
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/healthz") ||
        context.Request.Path.StartsWithSegments("/swagger"))
    {
        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
        }
    }
    else
    {
        context.Response.Headers.TryAdd(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://*.tile.openstreetmap.org https://server.arcgisonline.com https://*.tile.opentopomap.org; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'");
    }
    await next();
});

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
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

app.MapGet("/config.json", (IConfiguration configuration) =>
    Results.Json(new
    {
        apiKey = configuration["Frontend:ApiKey"] ?? configuration["Auth:ApiKey"] ?? string.Empty
    })).AllowAnonymous();

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
api.MapIntegrationEndpoints();
api.MapMessageEndpoints();
api.MapAlertRoutingEndpoints();
api.MapSensorEndpoints();
api.MapMaintenanceEndpoints();
api.MapCustodyEndpoints();
api.MapReportEndpoints();
api.MapAuditEndpoints();
api.MapIntegrationEventEndpoints();
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

if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html")))
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

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
