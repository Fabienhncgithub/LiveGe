using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontiereLiveGe.Api.Services;

public class AlertEngine : IAlertEngine
{
    private readonly AppDbContext _db;
    private readonly IMessageFormatter _formatter;
    private readonly ILogger<AlertEngine> _logger;

    public AlertEngine(AppDbContext db, IMessageFormatter formatter, ILogger<AlertEngine> logger)
    {
        _db = db;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<AlertEvent?> EvaluateAsync(BorderPoint borderPoint, TrafficSnapshot latest, TrendResult trend, BotSettings settings, CancellationToken ct)
    {
        var warningDelayThreshold = Math.Max(20, settings.CriticalDelayMinutes - 10);
        var significantRise = trend.Trend == TrendDirection.Rising
            && trend.DeltaMinutes >= settings.RisingThresholdMinutes
            && latest.EstimatedDelayMinutes >= 15;
        var shouldCreate = latest.EstimatedDelayMinutes >= warningDelayThreshold || significantRise;

        if (!shouldCreate)
        {
            return null;
        }

        var severity = GetSeverity(latest.EstimatedDelayMinutes, warningDelayThreshold, settings.CriticalDelayMinutes);
        var fingerprint = BuildFingerprint(borderPoint.Id, latest.CongestionLevel, trend.Trend, latest.EstimatedDelayMinutes);
        var categoryPrefix = BuildCategoryPrefix(borderPoint.Id, latest.CongestionLevel, trend.Trend);

        // Anti-doublon sur une fenetre longue: utile pour limiter les posts X payants.
        var recentSince = DateTime.UtcNow.AddMinutes(-90);
        var recentAlerts = await _db.AlertEvents
            .AsNoTracking()
            .Where(x => x.BorderPointId == borderPoint.Id && x.CreatedAtUtc >= recentSince)
            .ToListAsync(ct);

        if (recentAlerts.Any(x => x.Fingerprint == fingerprint))
        {
            _logger.LogInformation("Skipping duplicate alert for {BorderPoint} (fingerprint).", borderPoint.Name);
            return null;
        }

        if (recentAlerts.Any(x => x.Fingerprint.StartsWith(categoryPrefix, StringComparison.Ordinal)))
        {
            _logger.LogInformation("Skipping duplicate alert for {BorderPoint} (category + trend).", borderPoint.Name);
            return null;
        }

        var message = _formatter.FormatAlert(borderPoint, latest, trend);

        return new AlertEvent
        {
            BorderPointId = borderPoint.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Message = message,
            Severity = severity,
            Trend = trend.Trend,
            IsPosted = false,
            PostedAtUtc = null,
            Fingerprint = fingerprint,
            PredictedDelayMinutes = trend.PredictedDelayMinutes
        };
    }

    private static AlertSeverity GetSeverity(int delayMinutes, int warningThreshold, int criticalThreshold)
    {
        if (delayMinutes >= criticalThreshold)
        {
            return AlertSeverity.Critical;
        }

        if (delayMinutes >= warningThreshold)
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Info;
    }

    private static string BuildFingerprint(int borderPointId, CongestionLevel level, TrendDirection trend, int delayMinutes)
    {
        var bucket = delayMinutes / 5;
        return $"{borderPointId}|{level}|{trend}|{bucket}";
    }

    private static string BuildCategoryPrefix(int borderPointId, CongestionLevel level, TrendDirection trend)
    {
        return $"{borderPointId}|{level}|{trend}|";
    }
}
