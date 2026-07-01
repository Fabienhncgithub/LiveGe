namespace FrontiereLiveGe.Api.Services;

public interface IBorderRadarRunner
{
    Task<BorderRadarRunResult> RunAsync(CancellationToken ct);
}
