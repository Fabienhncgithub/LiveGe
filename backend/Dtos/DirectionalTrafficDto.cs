namespace FrontiereLiveGe.Api.Dtos;

public sealed class DirectionalTrafficDto
{
    public string BorderPointName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string DirectionLabel { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int? TravelTimeMinutes { get; set; }
    public int? FreeFlowTimeMinutes { get; set; }
    public int? DelayMinutes { get; set; }
    public string CongestionLevel { get; set; } = "Unknown";
    public string Trend { get; set; } = "Unknown";
    public string SourceName { get; set; } = "TomTom Traffic";
    public DateTime? ObservedAtUtc { get; set; }
    public bool IsStale { get; set; }
    public int? AgeMinutes { get; set; }
    public int ConfidencePercent { get; set; }
    public string? UnavailableReason { get; set; }
}
