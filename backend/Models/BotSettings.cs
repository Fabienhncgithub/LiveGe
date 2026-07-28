namespace FrontiereLiveGe.Api.Models;

public class BotSettings
{
    public int Id { get; set; }
    public bool PostingEnabled { get; set; }
    public int MinMinutesBetweenPosts { get; set; } = 60;
    public int RisingThresholdMinutes { get; set; } = 10;
    public int CriticalDelayMinutes { get; set; } = 30;
}
