namespace FrontiereLiveGe.Api.Dtos;

public sealed class MobilityAdviceDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public string AlgorithmVersion { get; set; } = "fusion-v1";
    public string ScopeNotice { get; set; } =
        "Comparaison des approches frontalières. Le trajet complet dépend encore de votre départ et de votre destination.";
    public List<RouteAdviceDto> Routes { get; set; } = new();
    public List<DataSourceStatusDto> Sources { get; set; } = new();
    public List<RoadSignalDto> Signals { get; set; } = new();
}

public sealed class RouteAdviceDto
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
    public DateTime? ObservedAtUtc { get; set; }
    public bool IsStale { get; set; }
    public int? AgeMinutes { get; set; }
    public int DataCoveragePercent { get; set; }
    public int ContextRiskPoints { get; set; }
    public int DecisionCost { get; set; }
    public string Recommendation { get; set; } = "Unavailable";
    public int? DelayAdvantageMinutes { get; set; }
    public List<AdviceReasonDto> Reasons { get; set; } = new();
    public List<string> NearbySignalIds { get; set; } = new();
    public string? UnavailableReason { get; set; }
}

public sealed class AdviceReasonDto
{
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
}
