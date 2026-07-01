using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public class FakeTrafficDataProvider : ITrafficDataProvider
{
    private static readonly string[] BorderPoints =
    {
        "Ani\u00e8res",
        "Bardonnex",
        "Ferney-Voltaire",
        "Meyrin",
        "Perly",
        "Moillesulaz",
        "Th\u00f4nex-Vallard"
    };

    private readonly ILogger<FakeTrafficDataProvider> _logger;

    public FakeTrafficDataProvider(ILogger<FakeTrafficDataProvider> logger)
    {
        _logger = logger;
    }

    public Task<List<TrafficReadingDto>> GetCurrentReadingsAsync(CancellationToken ct)
    {
        // Simule un trafic réaliste selon l'heure locale à Genève.
        var nowLocal = GetZurichNow();
        var isWeekend = nowLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var minutesOfDay = nowLocal.Hour * 60 + nowLocal.Minute;
        var timeBucket = (int)(new DateTimeOffset(nowLocal).ToUnixTimeSeconds() / 60);

        var readings = new List<TrafficReadingDto>();

        for (var i = 0; i < BorderPoints.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var name = BorderPoints[i];
            var rushFactor = GetRushFactor(name, minutesOfDay, isWeekend);
            var baseDelay = isWeekend ? 4 : 6;
            var offset = name switch
            {
                "Ani\u00e8res" => 3,
                "Bardonnex" => 2,
                "Ferney-Voltaire" => 2,
                "Meyrin" => 1,
                "Perly" => 1,
                "Moillesulaz" => 0,
                "Th\u00f4nex-Vallard" => 0,
                _ => 0
            };

            var rng = new Random(timeBucket + (i + 1) * 73);
            var noise = rng.Next(-2, 4);
            var delay = (int)Math.Round(baseDelay + offset + rushFactor * 12 + noise);
            delay = Math.Clamp(delay, 0, 45);

            var speedNoise = rng.Next(-3, 4);
            var speed = Math.Clamp(75 - delay * 2 + speedNoise, 10, 90);

            readings.Add(new TrafficReadingDto
            {
                BorderPointName = name,
                EstimatedDelayMinutes = delay,
                SpeedKmh = speed,
                SourceName = "FakeTraffic"
            });
        }

        _logger.LogDebug("Generated {Count} fake readings for {LocalTime}.", readings.Count, nowLocal);

        return Task.FromResult(readings);
    }

    private static double GetRushFactor(string borderPointName, int minutesOfDay, bool isWeekend)
    {
        // Week-end plus calme, avec de petits pics en fin de matinée et en fin d'après-midi.
        if (isWeekend)
        {
            if (IsInRange(minutesOfDay, 11 * 60, 13 * 60) || IsInRange(minutesOfDay, 17 * 60, 19 * 60))
            {
                return 0.4;
            }

            return 0.1;
        }

        if (IsInRange(minutesOfDay, 6 * 60 + 30, 9 * 60))
        {
            return borderPointName is "Bardonnex" or "Perly" or "Meyrin" or "Ferney-Voltaire" ? 1.0 : 0.6;
        }

        if (IsInRange(minutesOfDay, 16 * 60 + 30, 19 * 60))
        {
            return borderPointName is "Moillesulaz" or "Th\u00f4nex-Vallard" or "Ani\u00e8res" ? 1.0 : 0.8;
        }

        return 0.2;
    }

    private static bool IsInRange(int value, int startInclusive, int endInclusive)
    {
        return value >= startInclusive && value <= endInclusive;
    }

    private static DateTime GetZurichNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }
}
