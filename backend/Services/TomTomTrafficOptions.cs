namespace FrontiereLiveGe.Api.Services;

public sealed class TomTomTrafficOptions
{
    public const string SectionName = "Traffic:TomTom";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.tomtom.com/routing/1/";
    public int CacheSeconds { get; set; } = 1800;
    public int MaxRequestsPerMonth { get; set; } = 18000;
    public string BudgetStatePath { get; set; } = "data/tomtom-usage-budget.txt";
    public int WarningThresholdPercent { get; set; } = 75;
    public int CriticalThresholdPercent { get; set; } = 90;
}
