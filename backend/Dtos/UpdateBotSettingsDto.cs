namespace FrontiereLiveGe.Api.Dtos;

public class UpdateBotSettingsDto
{
    public bool PostingEnabled { get; set; }
    public int MinMinutesBetweenPosts { get; set; }
    public int RisingThresholdMinutes { get; set; }
    public int CriticalDelayMinutes { get; set; }
}
