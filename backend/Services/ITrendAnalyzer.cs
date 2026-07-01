namespace FrontiereLiveGe.Api.Services;

public interface ITrendAnalyzer
{
    Task<TrendResult> AnalyzeAsync(int borderPointId, CancellationToken ct);
}
