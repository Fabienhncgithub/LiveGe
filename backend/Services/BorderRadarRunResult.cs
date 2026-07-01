namespace FrontiereLiveGe.Api.Services;

public class BorderRadarRunResult
{
    public int SnapshotsCreated { get; set; }
    public int AlertsCreated { get; set; }
    public int AlertsPosted { get; set; }
    public DateTime RanAtUtc { get; set; } = DateTime.UtcNow;
}
