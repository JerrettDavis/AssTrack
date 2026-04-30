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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Identifier).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.Property(x => x.Protocol).IsRequired().HasMaxLength(20);
            entity.HasIndex(x => x.Identifier).IsUnique();
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Observation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Metadata).HasColumnType("TEXT");
            entity.HasIndex(x => new { x.DeviceId, x.ObservedAt });
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
    }
}
