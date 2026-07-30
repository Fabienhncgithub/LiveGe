using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Dtos;

public class TrafficReadingDto
{
    public string BorderPointName { get; set; } = string.Empty;
    public int EstimatedDelayMinutes { get; set; }
    public int SpeedKmh { get; set; }
    public CongestionLevel CongestionLevel { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
