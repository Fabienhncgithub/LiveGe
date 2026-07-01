using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public interface IAlertEngine
{
    Task<AlertEvent?> EvaluateAsync(BorderPoint borderPoint, TrafficSnapshot latest, TrendResult trend, BotSettings settings, CancellationToken ct);
}
