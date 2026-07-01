using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public interface IMessageFormatter
{
    string FormatAlert(BorderPoint borderPoint, TrafficSnapshot latest, TrendResult trend);
}
