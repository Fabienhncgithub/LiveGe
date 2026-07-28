using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Services;

namespace FrontiereLiveGe.Api.Tests;

public class CongestionCalculatorTests
{
    [Theory]
    [InlineData(0, CongestionLevel.Green)]
    [InlineData(9, CongestionLevel.Green)]
    [InlineData(10, CongestionLevel.Orange)]
    [InlineData(24, CongestionLevel.Orange)]
    [InlineData(25, CongestionLevel.Red)]
    public void Calculate_UsesExpectedThresholds(int delayMinutes, CongestionLevel expected)
    {
        Assert.Equal(expected, CongestionCalculator.Calculate(delayMinutes));
    }
}
