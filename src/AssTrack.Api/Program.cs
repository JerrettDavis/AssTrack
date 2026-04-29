using AssTrack.Api.Endpoints;
using AssTrack.Infrastructure.Data;
using AssTrack.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AssTrack") ?? "Data Source=asstrack.db";

builder.Services.AddDbContext<AssTrackDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AssetRepository>();
builder.Services.AddScoped<DeviceRepository>();
builder.Services.AddScoped<ObservationRepository>();
builder.Services.AddScoped<GeofenceRepository>();
builder.Services.AddScoped<SpeedAlertRepository>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseCors("Frontend");
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api");
api.MapAssetEndpoints();
api.MapDeviceEndpoints();
api.MapObservationEndpoints();
api.MapGeofenceEndpoints();
api.MapSpeedAlertEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program;
