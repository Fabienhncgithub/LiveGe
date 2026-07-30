using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public interface IDirectionalTrafficService
{
    Task<IReadOnlyList<DirectionalTrafficDto>> GetCurrentAsync(CancellationToken ct);
    HereQuotaStatusDto GetQuotaStatus();
}
