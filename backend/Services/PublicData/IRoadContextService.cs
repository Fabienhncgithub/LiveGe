using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public interface IRoadContextService
{
    Task<RoadContextDto> GetCurrentAsync(CancellationToken ct);
}
