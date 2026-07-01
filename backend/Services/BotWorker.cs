using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Services;

public class BotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotWorker> _logger;
    private readonly BotWorkerOptions _options;

    public BotWorker(IServiceProvider serviceProvider, IOptions<BotWorkerOptions> options, ILogger<BotWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, _options.IntervalMinutes);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("BotWorker started. Interval: {IntervalMinutes} minutes.", intervalMinutes);

        await RunCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IBorderRadarRunner>();
            await runner.RunAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BotWorker cycle canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotWorker cycle failed.");
        }
    }
}
