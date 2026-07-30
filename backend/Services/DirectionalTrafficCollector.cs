namespace FrontiereLiveGe.Api.Services;

public sealed class DirectionalTrafficCollector : BackgroundService
{
    private readonly IDirectionalTrafficService _traffic;
    private readonly ILogger<DirectionalTrafficCollector> _logger;

    public DirectionalTrafficCollector(
        IDirectionalTrafficService traffic,
        ILogger<DirectionalTrafficCollector> logger)
    {
        _traffic = traffic;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CollectAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CollectAsync(stoppingToken);
        }
    }

    private async Task CollectAsync(CancellationToken ct)
    {
        try
        {
            await _traffic.GetCurrentAsync(ct);
            var quota = _traffic.GetQuotaStatus();
            if (quota.Level is "Warning" or "Critical")
            {
                _logger.LogWarning(
                    "HERE quota alert: {Used}/{Limit} requests ({Percent}%). {Message}",
                    quota.RequestsUsed,
                    quota.DailyLimit,
                    quota.UsagePercent,
                    quota.Message);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directional HERE collection failed.");
        }
    }
}
