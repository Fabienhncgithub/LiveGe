using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Models;

public class AlertEvent
{
    public int Id { get; set; }
    public int BorderPointId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public TrendDirection Trend { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int? PredictedDelayMinutes { get; set; }

    public BorderPoint? BorderPoint { get; set; }
}
