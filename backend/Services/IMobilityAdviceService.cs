using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services;

public interface IMobilityAdviceService
{
    Task<MobilityAdviceDto> GetCurrentAsync(CancellationToken ct);
}
