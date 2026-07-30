using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontiereLiveGe.Api.Services;

public class TrendAnalyzer : ITrendAnalyzer
{
    private readonly AppDbContext _db;

    public TrendAnalyzer(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrendResult> AnalyzeAsync(
        int borderPointId,
        string? sourceName,
        CancellationToken ct)
    {
        var recentSince = DateTime.UtcNow.AddHours(-3);
        var snapshots = await _db.TrafficSnapshots
            .AsNoTracking()
            .Where(x => x.BorderPointId == borderPointId
                && (sourceName == null || x.SourceName == sourceName)
                && x.RecordedAtUtc >= recentSince)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Take(3)
            .ToListAsync(ct);

        if (snapshots.Count == 0)
        {
            return new TrendResult
            {
                Trend = TrendDirection.Stable,
                CurrentDelayMinutes = 0,
                PredictedDelayMinutes = 0,
                PredictionLabel = "stable",
                DeltaMinutes = 0
            };
        }

        snapshots.Reverse();
        var current = snapshots[^1].EstimatedDelayMinutes;
        var deltaMinutes = snapshots.Count >= 2
            ? current - snapshots[0].EstimatedDelayMinutes
            : 0;

        var trend = TrendDirection.Stable;
        if (snapshots.Count >= 3)
        {
            var s1 = snapshots[0].EstimatedDelayMinutes;
            var s2 = snapshots[1].EstimatedDelayMinutes;
            var s3 = snapshots[2].EstimatedDelayMinutes;

            if (s1 < s2 && s2 < s3)
            {
                trend = TrendDirection.Rising;
            }
            else if (s1 > s2 && s2 > s3)
            {
                trend = TrendDirection.Falling;
            }
        }

        var predicted = PredictDelayMinutes(snapshots, trend, current);
        var label = trend switch
        {
            TrendDirection.Rising => "hausse probable",
            TrendDirection.Falling => "am\u00e9lioration probable",
            _ => "stable"
        };

        return new TrendResult
        {
            Trend = trend,
            CurrentDelayMinutes = current,
            PredictedDelayMinutes = predicted,
            PredictionLabel = label,
            DeltaMinutes = deltaMinutes
        };
    }

    private static int PredictDelayMinutes(IReadOnlyList<TrafficSnapshot> snapshots, TrendDirection trend, int current)
    {
        if (snapshots.Count < 3)
        {
            return Math.Max(0, current);
        }

        var d1 = snapshots[1].EstimatedDelayMinutes - snapshots[0].EstimatedDelayMinutes;
        var d2 = snapshots[2].EstimatedDelayMinutes - snapshots[1].EstimatedDelayMinutes;
        var avgDelta = (d1 + d2) / 2.0;

        const double projectionFactor = 1.0; // next collection cycle (40 minutes by default)
        var predicted = current;

        if (trend == TrendDirection.Rising || trend == TrendDirection.Falling)
        {
            predicted = (int)Math.Round(current + avgDelta * projectionFactor);
        }

        return Math.Max(0, predicted);
    }
}
