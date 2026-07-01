using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public class FakePostPublisher : IPostPublisher
{
    private readonly ILogger<FakePostPublisher> _logger;

    public FakePostPublisher(ILogger<FakePostPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(AlertEvent alert, string message, CancellationToken ct)
    {
        _logger.LogInformation("[FAKE-PUBLISH] Alert {AlertId}: {Message}", alert.Id, message);
        return Task.CompletedTask;
    }
}
