using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public interface IPostPublisher
{
    Task PublishAsync(AlertEvent alert, string message, CancellationToken ct);
}
