using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Enums;

namespace FrontiereLiveGe.Api.Services;

public sealed class HereTrafficDataProvider : ITrafficDataProvider
{
    private readonly IDirectionalTrafficService _traffic;
    private readonly ILogger<HereTrafficDataProvider> _logger;

    public HereTrafficDataProvider(
        IDirectionalTrafficService traffic,
        ILogger<HereTrafficDataProvider> logger)
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
                SourceName = $"HERE:{x.Direction}",
                RecordedAtUtc = x.ObservedAtUtc!.Value
            })
            .ToList();

        _logger.LogInformation("Prepared {Count} real HERE readings for ingestion.", readings.Count);
        return readings;
    }
}
