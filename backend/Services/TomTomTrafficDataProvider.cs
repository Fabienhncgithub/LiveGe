using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Services;

public sealed class TomTomTrafficDataProvider : ITrafficDataProvider
{
    private readonly IDirectionalTrafficService _traffic;
    private readonly ILogger<TomTomTrafficDataProvider> _logger;

    public TomTomTrafficDataProvider(
        IDirectionalTrafficService traffic,
        ILogger<TomTomTrafficDataProvider> logger)
    {
        _traffic = traffic;
        _logger = logger;
    }

    public async Task<List<TrafficReadingDto>> GetCurrentReadingsAsync(CancellationToken ct)
    {
        var directions = await _traffic.RefreshAsync(ct);
        var readings = directions
            .Where(x => x.IsAvailable && x.ObservedAtUtc.HasValue)
            .Select(x => new TrafficReadingDto
            {
                BorderPointName = x.BorderPointName,
                EstimatedDelayMinutes = x.DelayMinutes ?? 0,
                SpeedKmh = 0,
                CongestionLevel = Enum.TryParse<CongestionLevel>(x.CongestionLevel, out var level)
                    ? level
                    : CongestionLevel.Green,
                SourceName = $"TOMTOM:{x.Direction}",
                RecordedAtUtc = x.ObservedAtUtc!.Value
            })
            .ToList();

        _logger.LogInformation("Prepared {Count} real TomTom readings for ingestion.", readings.Count);
        return readings;
    }
}
