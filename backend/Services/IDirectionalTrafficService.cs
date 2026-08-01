using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public interface IDirectionalTrafficService
{
    Task<IReadOnlyList<DirectionalTrafficDto>> GetCachedAsync(CancellationToken ct);
    Task<IReadOnlyList<DirectionalTrafficDto>> RefreshAsync(CancellationToken ct);
    TrafficQuotaStatusDto GetQuotaStatus();
}
