namespace FrontiereLiveGe.Api.Dtos;

public sealed class RoadContextDto
{
    public DateTime CheckedAtUtc { get; set; }
    public List<DataSourceStatusDto> Sources { get; set; } = new();
    public List<RoadSignalDto> Signals { get; set; } = new();
}

public sealed class DataSourceStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unavailable";
    public bool IsOfficial { get; set; }
    public bool HasBillingRisk { get; set; }
    public int RecordsCount { get; set; }
    public int RelevantSignalsCount { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public DateTime? DataTimestampUtc { get; set; }
    public string Coverage { get; set; } = string.Empty;
    public string Attribution { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class RoadSignalDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? TravelDirectionDegrees { get; set; }
    public bool AppliesToAllRoutes { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public DateTime? ObservedAtUtc { get; set; }
    public string? DetailsUrl { get; set; }
}
