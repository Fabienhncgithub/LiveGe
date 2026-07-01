using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;
using FrontiereLiveGe.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FrontiereLiveGe.Api.Data;

public class DbInitializer
{
    private readonly AppDbContext _db;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(AppDbContext db, ILogger<DbInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var migrations = _db.Database.GetService<IMigrationsAssembly>().Migrations;
            if (migrations.Count == 0)
            {
                // When no migrations exist yet, fall back to EnsureCreated for a clean local setup.
                await _db.Database.EnsureCreatedAsync();
            }
            else
            {
                await _db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Migration failed, falling back to EnsureCreated.");
            await _db.Database.EnsureCreatedAsync();
        }

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        // Guarantee tables exist even when no migrations are present.
        await _db.Database.EnsureCreatedAsync();
        if (!await TableExistsAsync("BorderPoints"))
        {
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
        }

        await UpsertBorderPointsAsync();

        var settings = await _db.BotSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            _db.BotSettings.Add(new BotSettings
            {
                PostingEnabled = true,
                MinMinutesBetweenPosts = 60,
                RisingThresholdMinutes = 10,
                CriticalDelayMinutes = 30
            });
        }
        else
        {
            settings.MinMinutesBetweenPosts = Math.Max(settings.MinMinutesBetweenPosts, 60);
            settings.RisingThresholdMinutes = Math.Max(settings.RisingThresholdMinutes, 10);
            settings.CriticalDelayMinutes = Math.Max(settings.CriticalDelayMinutes, 30);
        }

        await _db.SaveChangesAsync();

        if (!await _db.TrafficSnapshots.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var points = await _db.BorderPoints.ToListAsync();

            foreach (var point in points)
            {
                var delay = point.Name switch
                {
                    "Bardonnex" => 8,
                    "Perly" => 12,
                    "Moillesulaz" => 6,
                    "Th\u00f4nex-Vallard" => 7,
                    _ => 5
                };

                var speed = Math.Clamp(70 - delay * 2, 10, 80);

                _db.TrafficSnapshots.Add(new TrafficSnapshot
                {
                    BorderPointId = point.Id,
                    RecordedAtUtc = now,
                    EstimatedDelayMinutes = delay,
                    SpeedKmh = speed,
                    CongestionLevel = CongestionCalculator.Calculate(delay),
                    SourceName = "Seed"
                });
            }

            await _db.SaveChangesAsync();
        }
    }

    private async Task UpsertBorderPointsAsync()
    {
        var expectedPoints = new[]
        {
            new BorderPoint { Name = "Bardonnex", Latitude = 46.1406, Longitude = 6.1279, IsActive = true },
            new BorderPoint { Name = "Perly", Latitude = 46.1083, Longitude = 6.0754, IsActive = true },
            new BorderPoint { Name = "Moillesulaz", Latitude = 46.1876, Longitude = 6.2101, IsActive = true },
            new BorderPoint { Name = "Th\u00f4nex-Vallard", Latitude = 46.1935, Longitude = 6.2156, IsActive = true },
            new BorderPoint { Name = "Ani\u00e8res", Latitude = 46.2760, Longitude = 6.2220, IsActive = true },
            new BorderPoint { Name = "Meyrin", Latitude = 46.2340, Longitude = 6.0790, IsActive = true },
            new BorderPoint { Name = "Ferney-Voltaire", Latitude = 46.2550, Longitude = 6.1080, IsActive = true }
        };

        var existingByName = await _db.BorderPoints.ToDictionaryAsync(x => x.Name);

        foreach (var point in expectedPoints)
        {
            if (existingByName.TryGetValue(point.Name, out var existing))
            {
                existing.Latitude = point.Latitude;
                existing.Longitude = point.Longitude;
                existing.IsActive = true;
                continue;
            }

            _db.BorderPoints.Add(point);
        }
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "$name";
            nameParam.Value = tableName;
            command.Parameters.Add(nameParam);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
