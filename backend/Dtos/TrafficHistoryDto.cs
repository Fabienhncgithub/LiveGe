namespace FrontiereLiveGe.Api.Dtos;

public sealed class TrafficHistoryDto
{
    public int Id { get; set; }
    public string BorderPointName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string DirectionLabel { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
    public int DelayMinutes { get; set; }
    public string CongestionLevel { get; set; } = "Unknown";
}
