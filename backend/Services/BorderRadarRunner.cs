using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontiereLiveGe.Api.Services;

public class BorderRadarRunner : IBorderRadarRunner
{
    private readonly AppDbContext _db;
    private readonly ITrafficIngestionService _ingestionService;
    private readonly ITrendAnalyzer _trendAnalyzer;
    private readonly IAlertEngine _alertEngine;
    private readonly IPostPublisher _postPublisher;
    private readonly ILogger<BorderRadarRunner> _logger;

    public BorderRadarRunner(
        AppDbContext db,
        ITrafficIngestionService ingestionService,
        ITrendAnalyzer trendAnalyzer,
        IAlertEngine alertEngine,
        IPostPublisher postPublisher,
        ILogger<BorderRadarRunner> logger)
    {
        _db = db;
        _ingestionService = ingestionService;
        _trendAnalyzer = trendAnalyzer;
        _alertEngine = alertEngine;
        _postPublisher = postPublisher;
        _logger = logger;
    }

    public async Task<BorderRadarRunResult> RunAsync(CancellationToken ct)
    {
        var result = new BorderRadarRunResult
        {
            RanAtUtc = DateTime.UtcNow
        };

        var snapshots = await _ingestionService.IngestAsync(ct);
        result.SnapshotsCreated = snapshots.Count;

        if (snapshots.Count == 0)
        {
            _logger.LogWarning("Border radar run completed with no snapshots.");
            return result;
        }

        var settings = await GetOrCreateSettingsAsync(ct);
        var borderPoints = await _db.BorderPoints.AsNoTracking().ToListAsync(ct);
        var borderById = borderPoints.ToDictionary(x => x.Id);

        var alertsToCreate = new List<AlertEvent>();

        foreach (var snapshot in snapshots)
        {
            ct.ThrowIfCancellationRequested();

            if (!borderById.TryGetValue(snapshot.BorderPointId, out var borderPoint))
            {
                continue;
            }

            var trend = await _trendAnalyzer.AnalyzeAsync(snapshot.BorderPointId, ct);
            var alert = await _alertEngine.EvaluateAsync(borderPoint, snapshot, trend, settings, ct);

            if (alert is not null)
            {
                alertsToCreate.Add(alert);
            }
        }

        if (alertsToCreate.Count > 0)
        {
            await _db.AlertEvents.AddRangeAsync(alertsToCreate, ct);
            await _db.SaveChangesAsync(ct);
        }

        result.AlertsCreated = alertsToCreate.Count;

        if (settings.PostingEnabled && alertsToCreate.Count > 0)
        {
            result.AlertsPosted = await PublishAlertsAsync(alertsToCreate, settings, ct);
        }

        _logger.LogInformation("Border radar run finished. Snapshots: {Snapshots}, Alerts: {Alerts}, Posted: {Posted}.",
            result.SnapshotsCreated, result.AlertsCreated, result.AlertsPosted);

        return result;
    }

    private async Task<BotSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.BotSettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = new BotSettings
        {
            PostingEnabled = true,
            MinMinutesBetweenPosts = 60,
            RisingThresholdMinutes = 10,
            CriticalDelayMinutes = 30
        };

        _db.BotSettings.Add(settings);
        await _db.SaveChangesAsync(ct);

        return settings;
    }

    private async Task<int> PublishAlertsAsync(List<AlertEvent> alerts, BotSettings settings, CancellationToken ct)
    {
        var postedCount = 0;
        var lastPostedAt = await _db.AlertEvents
            .AsNoTracking()
            .Where(x => x.IsPosted)
            .OrderByDescending(x => x.PostedAtUtc)
            .Select(x => x.PostedAtUtc)
            .FirstOrDefaultAsync(ct);

        foreach (var alert in alerts)
        {
            ct.ThrowIfCancellationRequested();

            if (lastPostedAt.HasValue)
            {
                var minutesSinceLast = (DateTime.UtcNow - lastPostedAt.Value).TotalMinutes;
                if (minutesSinceLast < settings.MinMinutesBetweenPosts)
                {
                    _logger.LogInformation("Posting cooldown active ({Minutes:F1} min).", minutesSinceLast);
                    break;
                }
            }

            if (alert.Severity < Enums.AlertSeverity.Warning)
            {
                _logger.LogInformation("Skipping X post for low-value alert {AlertId} ({Severity}).", alert.Id, alert.Severity);
                continue;
            }

            await _postPublisher.PublishAsync(alert, alert.Message, ct);
            alert.IsPosted = true;
            alert.PostedAtUtc = DateTime.UtcNow;
            lastPostedAt = alert.PostedAtUtc;
            postedCount++;
        }

        if (postedCount > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return postedCount;
    }
}
