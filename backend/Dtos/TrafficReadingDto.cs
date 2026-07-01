namespace FrontiereLiveGe.Api.Dtos;

public class TrafficReadingDto
{
    public string BorderPointName { get; set; } = string.Empty;
    public int EstimatedDelayMinutes { get; set; }
    public int SpeedKmh { get; set; }
    public string SourceName { get; set; } = string.Empty;
}
