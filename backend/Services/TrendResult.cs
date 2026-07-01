using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Services;

public class TrendResult
{
    public TrendDirection Trend { get; set; }
    public int CurrentDelayMinutes { get; set; }
    public int PredictedDelayMinutes { get; set; }
    public string PredictionLabel { get; set; } = string.Empty;
    public int DeltaMinutes { get; set; }
}
