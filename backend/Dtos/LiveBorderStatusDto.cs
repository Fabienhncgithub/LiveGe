using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Dtos;

public class LiveBorderStatusDto
{
    public int BorderPointId { get; set; }
    public string BorderPointName { get; set; } = string.Empty;
    public int EstimatedDelayMinutes { get; set; }
    public int SpeedKmh { get; set; }
    public CongestionLevel CongestionLevel { get; set; }
    public TrendDirection Trend { get; set; }
    public int? PredictedDelayMinutes { get; set; }
    public string PredictionLabel { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
