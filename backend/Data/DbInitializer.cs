using FrontiereLiveGe.Api.Models;
using FrontiereLiveGe.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FrontiereLiveGe.Api.Data;

public class DbInitializer
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public DbInitializer(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        var migrations = _db.Database.GetService<IMigrationsAssembly>().Migrations;
        if (migrations.Count == 0)
        {
            await _db.Database.EnsureCreatedAsync();
        }
        else
        {
            if (_configuration.GetValue<bool>("Database:AdoptLegacySchema"))
            {
                await AdoptLegacySchemaAsync(migrations.Keys.OrderBy(x => x).First());
            }

            await _db.Database.MigrateAsync();
        }

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        await UpsertBorderPointsAsync();

        var settings = await _db.BotSettings
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
        if (settings is null)
        {
            _db.BotSettings.Add(new BotSettings
            {
                PostingEnabled = false,
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

    private async Task AdoptLegacySchemaAsync(string initialMigrationId)
    {
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            var expectedTables = new[] { "BorderPoints", "TrafficSnapshots", "AlertEvents", "BotSettings" };
            foreach (var table in expectedTables)
            {
                if (!await TableExistsAsync(connection, table))
                {
                    return;
                }
            }

            if (await MigrationHistoryHasRowsAsync(connection))
            {
                return;
            }

            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            await createCommand.ExecuteNonQueryAsync();

            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, $productVersion);
                """;

            var migrationParameter = insertCommand.CreateParameter();
            migrationParameter.ParameterName = "$migrationId";
            migrationParameter.Value = initialMigrationId;
            insertCommand.Parameters.Add(migrationParameter);

            var versionParameter = insertCommand.CreateParameter();
            versionParameter.ParameterName = "$productVersion";
            versionParameter.Value = ProductInfo.GetVersion();
            insertCommand.Parameters.Add(versionParameter);

            await insertCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> MigrationHistoryHasRowsAsync(System.Data.Common.DbConnection connection)
    {
        if (!await TableExistsAsync(connection, "__EFMigrationsHistory"))
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory";""";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }
}
