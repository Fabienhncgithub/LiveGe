using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontiereLiveGe.Api.Services;

public class TrafficIngestionService : ITrafficIngestionService
{
    private readonly AppDbContext _db;
    private readonly ITrafficDataProvider _provider;
    private readonly ILogger<TrafficIngestionService> _logger;

    public TrafficIngestionService(AppDbContext db, ITrafficDataProvider provider, ILogger<TrafficIngestionService> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    public async Task<List<TrafficSnapshot>> IngestAsync(CancellationToken ct)
    {
        var readings = await _provider.GetCurrentReadingsAsync(ct);
        var borderPoints = await _db.BorderPoints.AsNoTracking().ToListAsync(ct);
        var borderByName = borderPoints.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var snapshots = new List<TrafficSnapshot>();
        var now = DateTime.UtcNow;

        foreach (var reading in readings)
        {
            if (!borderByName.TryGetValue(reading.BorderPointName, out var borderPoint))
            {
                _logger.LogWarning("Border point not found for reading {Name}.", reading.BorderPointName);
                continue;
            }

            var snapshot = new TrafficSnapshot
            {
                BorderPointId = borderPoint.Id,
                RecordedAtUtc = now,
                EstimatedDelayMinutes = reading.EstimatedDelayMinutes,
                SpeedKmh = reading.SpeedKmh,
                CongestionLevel = CongestionCalculator.Calculate(reading.EstimatedDelayMinutes),
                SourceName = reading.SourceName
            };

            snapshots.Add(snapshot);
        }

        if (snapshots.Count == 0)
        {
            _logger.LogWarning("No traffic snapshots created during ingestion.");
            return snapshots;
        }

        await _db.TrafficSnapshots.AddRangeAsync(snapshots, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Persisted {Count} traffic snapshots.", snapshots.Count);
        return snapshots;
    }
}
