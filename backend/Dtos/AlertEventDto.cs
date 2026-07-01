using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Dtos;

public class AlertEventDto
{
    public int Id { get; set; }
    public int BorderPointId { get; set; }
    public string BorderPointName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public TrendDirection Trend { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public int? PredictedDelayMinutes { get; set; }
}
