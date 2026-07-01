namespace FrontiereLiveGe.Api.Dtos;

public class RunSummaryDto
{
    public int SnapshotsCreated { get; set; }
    public int AlertsCreated { get; set; }
    public int AlertsPosted { get; set; }
    public DateTime RanAtUtc { get; set; }
}
