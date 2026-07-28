using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;
using FrontiereLiveGe.Api.Services;

namespace FrontiereLiveGe.Api.Tests;

public class MessageFormatterTests
{
    [Fact]
    public void FormatAlert_IncludesDelayTrendAndLocalHashtags()
    {
        var formatter = new MessageFormatter();
        var borderPoint = new BorderPoint { Name = "Bardonnex" };
        var snapshot = new TrafficSnapshot
        {
            EstimatedDelayMinutes = 28,
            RecordedAtUtc = new DateTime(2026, 7, 28, 8, 0, 0, DateTimeKind.Utc)
        };
        var trend = new TrendResult
        {
            Trend = TrendDirection.Rising,
            PredictionLabel = "hausse probable"
        };

        var message = formatter.FormatAlert(borderPoint, snapshot, trend);

        Assert.Contains("Bardonnex", message);
        Assert.Contains("28 min", message);
        Assert.Contains("Hausse probable", message);
        Assert.Contains("#Geneve", message);
        Assert.Contains("#Frontiere", message);
        Assert.DoesNotContain("#G7Evian", message);
    }
}
