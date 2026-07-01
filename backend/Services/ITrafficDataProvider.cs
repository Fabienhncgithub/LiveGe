using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public interface ITrafficDataProvider
{
    Task<List<TrafficReadingDto>> GetCurrentReadingsAsync(CancellationToken ct);
}
