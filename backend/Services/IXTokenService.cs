namespace FrontiereLiveGe.Api.Services;

public interface IXTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct);
    Task<bool> TryRefreshAsync(CancellationToken ct);
}
