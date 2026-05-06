using AssTrack.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AssTrack.Infrastructure.Data;

public class AssTrackDbContext(DbContextOptions<AssTrackDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Observation> Observations => Set<Observation>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<SpeedAlert> SpeedAlerts => Set<SpeedAlert>();
    public DbSet<GeofenceBreach> GeofenceBreaches => Set<GeofenceBreach>();
    public DbSet<DeviceGeofenceState> DeviceGeofenceStates => Set<DeviceGeofenceState>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<IntegrationFeed> IntegrationFeeds => Set<IntegrationFeed>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.SpeedThresholdKmh);
            entity.Property(x => x.IsSeeded).HasDefaultValue(false);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Identifier).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.Property(x => x.Protocol).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Provider).IsRequired().HasMaxLength(80).HasDefaultValue("manual");
            entity.Property(x => x.ExternalId).HasMaxLength(300);
            entity.Property(x => x.Tags).HasMaxLength(500);
            entity.Property(x => x.IsSeeded).HasDefaultValue(false);
            entity.HasIndex(x => x.Identifier).IsUnique();
            entity.HasIndex(x => new { x.IntegrationFeedId, x.ExternalId });
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.IntegrationFeed)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.IntegrationFeedId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IntegrationFeed>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Provider).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DefaultTags).HasMaxLength(500);
            entity.Property(x => x.ConfigurationJson).HasColumnType("TEXT");
            entity.HasIndex(x => x.Provider);
        });

        modelBuilder.Entity<Observation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Metadata).HasColumnType("TEXT");
            entity.HasIndex(x => new { x.DeviceId, x.ObservedAt }).IsUnique();
            entity.HasIndex(x => x.ObservedAt);
            entity.HasOne(x => x.Device)
                .WithMany(x => x.Observations)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Geofence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.ShapeType).IsRequired().HasMaxLength(32).HasDefaultValue("circle");
            entity.Property(x => x.PolygonJson).HasColumnType("TEXT");
            entity.Property(x => x.IsSeeded).HasDefaultValue(false);
        });

        modelBuilder.Entity<SpeedAlert>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ObservationId);
            entity.HasIndex(x => new { x.DeviceId, x.TriggeredAt });
            entity.HasOne(x => x.Observation)
                .WithMany()
                .HasForeignKey(x => x.ObservationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GeofenceBreach>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ObservationId);
            entity.HasIndex(x => new { x.DeviceId, x.DetectedAt });
            entity.HasOne(x => x.Observation)
                .WithMany()
                .HasForeignKey(x => x.ObservationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Geofence)
                .WithMany()
                .HasForeignKey(x => x.GeofenceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.EventType).HasDefaultValue(GeofenceBreachEventType.Enter);
        });

        modelBuilder.Entity<DeviceGeofenceState>(entity =>
        {
            entity.HasKey(x => new { x.DeviceId, x.GeofenceId });
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Geofence).WithMany().HasForeignKey(x => x.GeofenceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebhookDeliveryLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.TargetUrl).IsRequired().HasMaxLength(2000);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.RequestPayloadSummary).HasMaxLength(500);
            entity.Property(x => x.AttemptNumber).HasDefaultValue(1);
            entity.Property(x => x.CorrelationId).IsRequired().HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.HasIndex(x => x.AttemptedAt);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.CorrelationId);
        });
    }
}
