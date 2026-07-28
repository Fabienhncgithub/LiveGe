using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public interface IPostPublisher
{
    bool IsLive { get; }
    Task PublishAsync(AlertEvent alert, string message, CancellationToken ct);
}
