namespace FrontiereLiveGe.Api.Services;

public static class GenevaTime
{
    private static readonly Lazy<TimeZoneInfo> Zone = new(ResolveTimeZone);

    public static DateTime FromUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone.Value);
    }

    public static DateTime ToUtc(DateTime localValue)
    {
        var unspecified = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone.Value);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
