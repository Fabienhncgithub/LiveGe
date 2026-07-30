namespace FrontiereLiveGe.Api.Services;

public sealed class HereTrafficOptions
{
    public const string SectionName = "Traffic:Here";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://router.hereapi.com/v8/";
    public int CacheSeconds { get; set; } = 1800;
    public int MaxRequestsPerDay { get; set; } = 700;
    public string BudgetStatePath { get; set; } = "data/here-usage-budget.txt";
    public int WarningThresholdPercent { get; set; } = 75;
    public int CriticalThresholdPercent { get; set; } = 90;
}
