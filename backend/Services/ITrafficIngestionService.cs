using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public interface ITrafficIngestionService
{
    Task<List<TrafficSnapshot>> IngestAsync(CancellationToken ct);
}
