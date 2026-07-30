using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Services.PublicData;

namespace FrontiereLiveGe.Api.Services;

public sealed class MobilityAdviceService : IMobilityAdviceService
{
    private readonly IDirectionalTrafficService _traffic;
    private readonly IRoadContextService _roadContext;

    public MobilityAdviceService(
        IDirectionalTrafficService traffic,
        IRoadContextService roadContext)
    {
        _traffic = traffic;
        _roadContext = roadContext;
    }

    public async Task<MobilityAdviceDto> GetCurrentAsync(CancellationToken ct)
    {
        var trafficTask = _traffic.GetCachedAsync(ct);
        var contextTask = _roadContext.GetCurrentAsync(ct);
        await Task.WhenAll(trafficTask, contextTask);

        var traffic = await trafficTask;
        var context = await contextTask;
        var providerCoverage = CalculateProviderCoverage(context.Sources);
        var routes = traffic.Select(reading =>
            BuildRoute(reading, context.Signals, providerCoverage)).ToList();

        ApplyRecommendations(routes);

        var relevantSignalIds = routes
            .SelectMany(x => x.NearbySignalIds)
            .ToHashSet(StringComparer.Ordinal);
        var relevantSignals = context.Signals
            .Where(x => x.AppliesToAllRoutes || relevantSignalIds.Contains(x.Id))
            .Take(50)
            .ToList();
        var publicSources = context.Sources
            .Select(source => CopyWithRelevantCount(
                source,
                relevantSignals.Count(x => x.SourceId == source.Id)))
            .ToList();

        var quota = _traffic.GetQuotaStatus();
        var hereAvailable = traffic.Count(x => x.IsAvailable);
        var hereFresh = traffic.Count(x => x.IsAvailable && !x.IsStale);
        var hereStatus = new DataSourceStatusDto
        {
            Id = "here-traffic",
            Name = "HERE Traffic",
            Status = hereFresh > 0
                ? "Online"
                : hereAvailable > 0
                    ? "Stale"
                    : "Unavailable",
            IsOfficial = false,
            HasBillingRisk = true,
            RecordsCount = traffic.Count,
            RelevantSignalsCount = hereAvailable,
            CheckedAtUtc = DateTime.UtcNow,
            DataTimestampUtc = traffic.Where(x => x.ObservedAtUtc.HasValue)
                .Select(x => x.ObservedAtUtc)
                .Max(),
            Coverage = "Temps de parcours sur les routes d’approche, passage frontalier imposé.",
            Attribution = "Source : HERE Traffic",
            SourceUrl = "https://www.here.com/",
            Message = hereAvailable > 0
                ? $"{quota.RequestsUsed}/{quota.DailyLimit} appels locaux utilisés aujourd’hui."
                : "Aucune mesure HERE en cache. Le collecteur de fond doit réussir un cycle."
        };

        return new MobilityAdviceDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Routes = routes,
            Sources = [hereStatus, .. publicSources],
            Signals = relevantSignals
        };
    }

    internal static RouteAdviceDto BuildRoute(
        DirectionalTrafficDto reading,
        IReadOnlyList<RoadSignalDto> signals,
        int providerCoverage)
    {
        var corridor = BorderCorridorCatalog.Find(reading.BorderPointName);
        var relevant = corridor is null
            ? []
            : signals
                .Select(signal => ToRelevantSignal(corridor, signal, reading.Direction))
                .Where(x => x is not null)
                .Select(x => x!)
                .GroupBy(
                    x => $"{x.Signal.SourceId}|{x.Signal.Category}|{x.Signal.Title}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(x => SeverityPoints(x.Signal.Severity))
                    .ThenBy(x => x.DistanceKm)
                    .First())
                .OrderByDescending(x => SeverityPoints(x.Signal.Severity))
                .ThenBy(x => x.DistanceKm)
                .ToList();

        var contextRisk = Math.Min(60, relevant.Sum(RiskPoints));
        var coverage = reading.IsAvailable
            ? Math.Clamp((reading.IsStale ? 15 : 55) + providerCoverage, 0, 100)
            : 0;
        var reasons = new List<AdviceReasonDto>();

        if (reading.IsAvailable)
        {
            reasons.Add(new AdviceReasonDto
            {
                Kind = "Traffic",
                Label = reading.IsStale
                    ? $"+{reading.DelayMinutes ?? 0} min sur la dernière mesure disponible ({reading.AgeMinutes ?? 0} min)"
                    : $"+{reading.DelayMinutes ?? 0} min mesurées sur l’approche frontalière",
                SourceName = "HERE Traffic",
                Severity = reading.IsStale ? "Warning" : reading.CongestionLevel
            });
        }

        foreach (var signal in relevant.Take(2))
        {
            var distance = signal.Signal.AppliesToAllRoutes
                ? string.Empty
                : $" à {signal.DistanceKm:0.#} km de l’approche";
            reasons.Add(new AdviceReasonDto
            {
                Kind = signal.Signal.Category,
                Label = $"{signal.Signal.Title}{distance}",
                SourceName = signal.Signal.SourceName,
                Severity = signal.Signal.Severity
            });
        }

        if (reading.IsAvailable && relevant.Count == 0 && providerCoverage > 0)
        {
            reasons.Add(new AdviceReasonDto
            {
                Kind = "Coverage",
                Label = "Aucun signal proche trouvé dans les flux publics actuellement disponibles",
                SourceName = "Sources publiques",
                Severity = "Info"
            });
        }

        return new RouteAdviceDto
        {
            BorderPointName = reading.BorderPointName,
            Direction = reading.Direction,
            DirectionLabel = reading.DirectionLabel,
            IsAvailable = reading.IsAvailable,
            TravelTimeMinutes = reading.TravelTimeMinutes,
            FreeFlowTimeMinutes = reading.FreeFlowTimeMinutes,
            DelayMinutes = reading.DelayMinutes,
            CongestionLevel = reading.CongestionLevel,
            Trend = reading.Trend,
            ObservedAtUtc = reading.ObservedAtUtc,
            IsStale = reading.IsStale,
            AgeMinutes = reading.AgeMinutes,
            DataCoveragePercent = coverage,
            ContextRiskPoints = contextRisk,
            DecisionCost = reading.IsAvailable
                ? (reading.DelayMinutes ?? 0) * 4 + contextRisk
                : int.MaxValue,
            Reasons = reasons,
            NearbySignalIds = relevant.Select(x => x.Signal.Id).Distinct(StringComparer.Ordinal).ToList(),
            UnavailableReason = reading.UnavailableReason
        };
    }

    internal static void ApplyRecommendations(IReadOnlyCollection<RouteAdviceDto> routes)
    {
        foreach (var directionGroup in routes.GroupBy(x => x.Direction))
        {
            var available = directionGroup
                .Where(x => x.IsAvailable)
                .OrderBy(x => x.DecisionCost)
                .ThenBy(x => x.DelayMinutes)
                .ToList();
            if (available.Count == 0)
            {
                foreach (var route in directionGroup)
                {
                    route.Recommendation = "Unavailable";
                }

                continue;
            }

            var eligible = available
                .Where(x => !x.Reasons.Any(reason =>
                        reason.Severity == "Critical" && reason.Kind != "Traffic")
                    && x.DelayMinutes is < 15
                    && !x.IsStale)
                .ToList();
            var best = eligible.FirstOrDefault() ?? available[0];
            var comparisonPool = available.Where(x => !x.IsStale).ToList();
            var second = comparisonPool.FirstOrDefault(x => !ReferenceEquals(x, best));
            var lead = second is null ? 0 : second.DecisionCost - best.DecisionCost;
            best.DelayAdvantageMinutes = second is null
                ? null
                : Math.Max(0, (second.DelayMinutes ?? 0) - (best.DelayMinutes ?? 0));

            foreach (var route in available)
            {
                var hasCriticalSignal = route.Reasons.Any(x => x.Severity == "Critical" && x.Kind != "Traffic");
                if (hasCriticalSignal || route.DelayMinutes is >= 15)
                {
                    route.Recommendation = "Avoid";
                    continue;
                }

                if (route.IsStale)
                {
                    route.Recommendation = eligible.Count == 0 && ReferenceEquals(route, best)
                        ? "Equivalent"
                        : "Alternative";
                    continue;
                }

                if (ReferenceEquals(route, best))
                {
                    route.Recommendation = second is not null
                        && !route.IsStale
                        && lead >= 12
                        && route.DataCoveragePercent >= 70
                        ? "Recommended"
                        : "Equivalent";
                    continue;
                }

                route.Recommendation = route.DecisionCost - best.DecisionCost < 12
                    ? "Equivalent"
                    : "Alternative";
            }
        }
    }

    private static RelevantSignal? ToRelevantSignal(
        BorderCorridorCatalog.BorderCorridor corridor,
        RoadSignalDto signal,
        string direction)
    {
        if (signal.AppliesToAllRoutes)
        {
            return new RelevantSignal(signal, 0);
        }

        if (!signal.Latitude.HasValue || !signal.Longitude.HasValue)
        {
            return null;
        }

        if (signal.TravelDirectionDegrees.HasValue
            && !MatchesTravelDirection(corridor, direction, signal.TravelDirectionDegrees.Value))
        {
            return null;
        }

        var distance = BorderCorridorCatalog.DistanceToApproachKm(
            corridor,
            signal.Latitude.Value,
            signal.Longitude.Value);
        var maximumDistance = signal.SourceId switch
        {
            "sitg-roadworks" => 1.25d,
            "bison-fute-open" => 4d,
            _ => 2d
        };
        return distance <= maximumDistance ? new RelevantSignal(signal, distance) : null;
    }

    private static bool MatchesTravelDirection(
        BorderCorridorCatalog.BorderCorridor corridor,
        string direction,
        double signalBearing)
    {
        var routeBearing = BearingDegrees(corridor.France, corridor.Geneva);
        if (string.Equals(direction, "ToFrance", StringComparison.Ordinal))
        {
            routeBearing = (routeBearing + 180) % 360;
        }

        var difference = Math.Abs((signalBearing - routeBearing + 540) % 360 - 180);
        return difference < 90;
    }

    private static double BearingDegrees(
        BorderCorridorCatalog.Coordinate start,
        BorderCorridorCatalog.Coordinate end)
    {
        var latitude1 = start.Latitude * Math.PI / 180;
        var latitude2 = end.Latitude * Math.PI / 180;
        var longitudeDelta = (end.Longitude - start.Longitude) * Math.PI / 180;
        var y = Math.Sin(longitudeDelta) * Math.Cos(latitude2);
        var x = Math.Cos(latitude1) * Math.Sin(latitude2)
            - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitudeDelta);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static int RiskPoints(RelevantSignal relevant)
    {
        var basePoints = SeverityPoints(relevant.Signal.Severity);
        if (relevant.Signal.AppliesToAllRoutes)
        {
            return relevant.Signal.Severity switch
            {
                "Critical" => 10,
                "Warning" => 5,
                _ => 1
            };
        }

        var factor = relevant.DistanceKm switch
        {
            <= 1 => 1d,
            <= 2.5 => 0.75d,
            _ => 0.45d
        };
        return (int)Math.Ceiling(basePoints * factor);
    }

    private static int SeverityPoints(string severity) => severity switch
    {
        "Critical" => 20,
        "Warning" => 8,
        _ => 2
    };

    private static int CalculateProviderCoverage(IReadOnlyCollection<DataSourceStatusDto> sources)
    {
        var weights = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["sitg-roadworks"] = 20,
            ["bison-fute-open"] = 15,
            ["meteoswiss-gve"] = 10
        };

        return sources.Sum(source =>
        {
            if (!weights.TryGetValue(source.Id, out var weight))
            {
                return 0;
            }

            return source.Status switch
            {
                "Online" => weight,
                "Stale" => weight / 2,
                _ => 0
            };
        });
    }

    private static DataSourceStatusDto CopyWithRelevantCount(
        DataSourceStatusDto source,
        int relevantSignalsCount) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Status = source.Status,
            IsOfficial = source.IsOfficial,
            HasBillingRisk = source.HasBillingRisk,
            RecordsCount = source.RecordsCount,
            RelevantSignalsCount = relevantSignalsCount,
            CheckedAtUtc = source.CheckedAtUtc,
            DataTimestampUtc = source.DataTimestampUtc,
            Coverage = source.Coverage,
            Attribution = source.Attribution,
            SourceUrl = source.SourceUrl,
            Message = source.Message
        };

    private sealed record RelevantSignal(RoadSignalDto Signal, double DistanceKm);
}
