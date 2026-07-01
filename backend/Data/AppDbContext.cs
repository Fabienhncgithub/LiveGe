using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontiereLiveGe.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<BorderPoint> BorderPoints => Set<BorderPoint>();
    public DbSet<TrafficSnapshot> TrafficSnapshots => Set<TrafficSnapshot>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<BotSettings> BotSettings => Set<BotSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BorderPoint>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<TrafficSnapshot>()
            .HasIndex(x => new { x.BorderPointId, x.RecordedAtUtc });

        modelBuilder.Entity<AlertEvent>()
            .HasIndex(x => new { x.BorderPointId, x.CreatedAtUtc });

        modelBuilder.Entity<AlertEvent>()
            .HasIndex(x => x.Fingerprint);

        modelBuilder.Entity<BotSettings>()
            .HasIndex(x => x.Id);
    }
}
