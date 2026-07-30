namespace FrontiereLiveGe.Api.Dtos;

public sealed class TrafficForecastDto
{
    public bool IsAvailable { get; set; }
    public int SamplesCount { get; set; }
    public int DaysCovered { get; set; }
    public int MinimumDaysRequired { get; set; } = 7;
    public string Message { get; set; } = string.Empty;
    public List<TrafficForecastSuggestionDto> Suggestions { get; set; } = new();
}

public sealed class TrafficForecastSuggestionDto
{
    public string Direction { get; set; } = string.Empty;
    public string DirectionLabel { get; set; } = string.Empty;
    public string BestDay { get; set; } = string.Empty;
    public int BestHourStart { get; set; }
    public int AverageDelayMinutes { get; set; }
    public int SampleSize { get; set; }
    public int ConfidencePercent { get; set; }
    public string Advice { get; set; } = string.Empty;
}
