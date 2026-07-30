using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public interface IPublicDataProvider
{
    Task<PublicDataSnapshot> GetSnapshotAsync(CancellationToken ct);
}

public sealed class PublicDataSnapshot
{
    public required DataSourceStatusDto Source { get; init; }
    public IReadOnlyList<RoadSignalDto> Signals { get; init; } = [];
}
