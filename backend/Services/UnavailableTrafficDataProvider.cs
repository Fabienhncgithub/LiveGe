using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public sealed class UnavailableTrafficDataProvider : ITrafficDataProvider
{
    private readonly ILogger<UnavailableTrafficDataProvider> _logger;

    public UnavailableTrafficDataProvider(ILogger<UnavailableTrafficDataProvider> logger)
    {
        _logger = logger;
    }

    public Task<List<TrafficReadingDto>> GetCurrentReadingsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Legacy traffic ingestion skipped: no real provider is configured.");
        return Task.FromResult(new List<TrafficReadingDto>());
    }
}
