namespace FrontiereLiveGe.Api.Dtos;

public sealed class HereQuotaStatusDto
{
    public DateOnly DateUtc { get; set; }
    public int RequestsUsed { get; set; }
    public int DailyLimit { get; set; }
    public int RequestsRemaining { get; set; }
    public int UsagePercent { get; set; }
    public string Level { get; set; } = "Normal";
    public string Message { get; set; } = string.Empty;
    public DateTime ResetsAtUtc { get; set; }
}
