using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public sealed class RoadContextService : IRoadContextService
{
    private readonly IReadOnlyList<IPublicDataProvider> _providers;

    public RoadContextService(IEnumerable<IPublicDataProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<RoadContextDto> GetCurrentAsync(CancellationToken ct)
    {
        var snapshots = await Task.WhenAll(_providers.Select(x => x.GetSnapshotAsync(ct)));
        var signals = snapshots
            .SelectMany(x => x.Signals)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => SeverityOrder(x.Severity))
            .ThenByDescending(x => x.ObservedAtUtc)
            .Take(200)
            .ToList();

        return new RoadContextDto
        {
            CheckedAtUtc = DateTime.UtcNow,
            Sources = snapshots.Select(x => x.Source).ToList(),
            Signals = signals
        };
    }

    private static int SeverityOrder(string severity) => severity switch
    {
        "Critical" => 0,
        "Warning" => 1,
        _ => 2
    };
}
