using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Models;

public class TrafficSnapshot
{
    public int Id { get; set; }
    public int BorderPointId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public int EstimatedDelayMinutes { get; set; }
    public int SpeedKmh { get; set; }
    public CongestionLevel CongestionLevel { get; set; }
    public string SourceName { get; set; } = string.Empty;

    public BorderPoint? BorderPoint { get; set; }
}
