namespace FrontiereLiveGe.Api.Dtos;

public sealed class TrafficQuotaStatusDto
{
    public string MonthUtc { get; set; } = string.Empty;
    public int RequestsUsed { get; set; }
    public int MonthlyLimit { get; set; }
    public int RequestsRemaining { get; set; }
    public int UsagePercent { get; set; }
    public string Level { get; set; } = "Normal";
    public string Message { get; set; } = string.Empty;
    public DateTime ResetsAtUtc { get; set; }
}
