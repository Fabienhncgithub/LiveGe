using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Services;

public static class CongestionCalculator
{
    public static CongestionLevel Calculate(int estimatedDelayMinutes)
    {
        if (estimatedDelayMinutes < 10)
        {
            return CongestionLevel.Green;
        }

        if (estimatedDelayMinutes < 25)
        {
            return CongestionLevel.Orange;
        }

        return CongestionLevel.Red;
    }
}
