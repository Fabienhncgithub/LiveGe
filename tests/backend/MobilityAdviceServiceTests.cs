using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Services;

namespace FrontiereLiveGe.Api.Tests;

public sealed class MobilityAdviceServiceTests
{
    [Fact]
    public void BuildRoute_CombinesTomTomDelayWithNearbyAndGlobalContext()
    {
        var reading = Traffic("Bardonnex", "ToGeneva", delayMinutes: 4);
        var corridor = Assert.IsType<BorderCorridorCatalog.BorderCorridor>(
            BorderCorridorCatalog.Find("Bardonnex"));
        var signals = new List<RoadSignalDto>
        {
            Signal(
                id: "near-roadwork",
                severity: "Warning",
                latitude: corridor.Crossing.Latitude,
                longitude: corridor.Crossing.Longitude),
            Signal(
                id: "near-roadwork-duplicate",
                severity: "Warning",
                latitude: corridor.Crossing.Latitude,
                longitude: corridor.Crossing.Longitude),
            Signal(
                id: "remote-roadwork",
                severity: "Critical",
                latitude: 46.5197,
                longitude: 6.6323),
            new()
            {
                Id = "weather",
                SourceId = "meteoswiss-gve",
                SourceName = "MétéoSuisse — Cointrin",
                Category = "Weather",
                Severity = "Warning",
                Title = "Fortes rafales",
                AppliesToAllRoutes = true
            }
        };

        var route = MobilityAdviceService.BuildRoute(reading, signals, providerCoverage: 45);

        Assert.Equal(100, route.DataCoveragePercent);
        Assert.Equal(13, route.ContextRiskPoints);
        Assert.Equal(29, route.DecisionCost);
        Assert.Contains("near-roadwork", route.NearbySignalIds);
        Assert.DoesNotContain("near-roadwork-duplicate", route.NearbySignalIds);
        Assert.Contains("weather", route.NearbySignalIds);
        Assert.DoesNotContain("remote-roadwork", route.NearbySignalIds);
        Assert.Contains(route.Reasons, reason => reason.SourceName == "TomTom Traffic");
        Assert.Contains(route.Reasons, reason => reason.SourceName == "MétéoSuisse — Cointrin");
    }

    [Fact]
    public void ApplyRecommendations_RecommendsClearWinnerAndMarksCriticalRouteAvoid()
    {
        var winner = Route("Bardonnex", "ToGeneva", decisionCost: 8, delayMinutes: 2);
        var alternative = Route("Perly", "ToGeneva", decisionCost: 28, delayMinutes: 7);
        var critical = Route("Meyrin", "ToGeneva", decisionCost: 40, delayMinutes: 1);
        critical.Reasons.Add(new AdviceReasonDto
        {
            Kind = "Roadworks",
            Label = "Route fermée",
            SourceName = "InfoMobilité Genève",
            Severity = "Critical"
        });

        MobilityAdviceService.ApplyRecommendations([winner, alternative, critical]);

        Assert.Equal("Recommended", winner.Recommendation);
        Assert.Equal(5, winner.DelayAdvantageMinutes);
        Assert.Equal("Alternative", alternative.Recommendation);
        Assert.Equal("Avoid", critical.Recommendation);
    }

    [Fact]
    public void ApplyRecommendations_UsesEquivalentWhenScoresAreTooClose()
    {
        var first = Route("Bardonnex", "FromGeneva", decisionCost: 20, delayMinutes: 3);
        var second = Route("Perly", "FromGeneva", decisionCost: 29, delayMinutes: 4);

        MobilityAdviceService.ApplyRecommendations([first, second]);

        Assert.Equal("Equivalent", first.Recommendation);
        Assert.Equal("Equivalent", second.Recommendation);
    }

    [Fact]
    public void ApplyRecommendations_DoesNotUseAvoidedRouteAsTheBestComparison()
    {
        var delayed = Route("Bardonnex", "ToGeneva", decisionCost: 80, delayMinutes: 16);
        var viable = Route("Perly", "ToGeneva", decisionCost: 20, delayMinutes: 5);

        MobilityAdviceService.ApplyRecommendations([delayed, viable]);

        Assert.Equal("Avoid", delayed.Recommendation);
        Assert.Equal("Recommended", viable.Recommendation);
    }

    [Fact]
    public void ApplyRecommendations_DoesNotClaimWinnerWithoutAComparison()
    {
        var onlyRoute = Route("Bardonnex", "ToGeneva", decisionCost: 8, delayMinutes: 2);

        MobilityAdviceService.ApplyRecommendations([onlyRoute]);

        Assert.Equal("Equivalent", onlyRoute.Recommendation);
        Assert.Null(onlyRoute.DelayAdvantageMinutes);
    }

    [Fact]
    public void ApplyRecommendations_NeverRecommendsAStaleTrafficReading()
    {
        var stale = Route("Bardonnex", "ToGeneva", decisionCost: 8, delayMinutes: 2);
        stale.IsStale = true;

        MobilityAdviceService.ApplyRecommendations([stale]);

        Assert.Equal("Equivalent", stale.Recommendation);
    }

    [Fact]
    public void ApplyRecommendations_DowngradesStaleAlternativeWhenFreshDataExists()
    {
        var stale = Route("Bardonnex", "ToGeneva", decisionCost: 8, delayMinutes: 2);
        stale.IsStale = true;
        var fresh = Route("Perly", "ToGeneva", decisionCost: 20, delayMinutes: 5);

        MobilityAdviceService.ApplyRecommendations([stale, fresh]);

        Assert.Equal("Alternative", stale.Recommendation);
        Assert.Equal("Equivalent", fresh.Recommendation);
    }

    [Fact]
    public void BuildRoute_AppliesDirectionalBisonSignalOnlyToMatchingTravelDirection()
    {
        var corridor = Assert.IsType<BorderCorridorCatalog.BorderCorridor>(
            BorderCorridorCatalog.Find("Bardonnex"));
        var signal = new RoadSignalDto
        {
            Id = "northbound-event",
            SourceId = "bison-fute-open",
            SourceName = "Bison Futé — DIR",
            Category = "Accident",
            Severity = "Warning",
            Title = "Accident",
            Latitude = corridor.Crossing.Latitude,
            Longitude = corridor.Crossing.Longitude,
            TravelDirectionDegrees = 0
        };

        var toGeneva = MobilityAdviceService.BuildRoute(
            Traffic("Bardonnex", "ToGeneva", 2),
            [signal],
            providerCoverage: 45);
        var toFrance = MobilityAdviceService.BuildRoute(
            Traffic("Bardonnex", "ToFrance", 2),
            [signal],
            providerCoverage: 45);

        Assert.Contains(signal.Id, toGeneva.NearbySignalIds);
        Assert.DoesNotContain(signal.Id, toFrance.NearbySignalIds);
    }

    [Fact]
    public void BuildRoute_ReducesCoverageForStaleTomTomReading()
    {
        var reading = Traffic("Bardonnex", "ToGeneva", 2);
        reading.IsStale = true;

        var route = MobilityAdviceService.BuildRoute(reading, [], providerCoverage: 45);

        Assert.Equal(60, route.DataCoveragePercent);
    }

    private static DirectionalTrafficDto Traffic(
        string borderPointName,
        string direction,
        int delayMinutes) =>
        new()
        {
            BorderPointName = borderPointName,
            Direction = direction,
            DirectionLabel = "France → Genève",
            IsAvailable = true,
            TravelTimeMinutes = 12,
            FreeFlowTimeMinutes = 12 - delayMinutes,
            DelayMinutes = delayMinutes,
            CongestionLevel = "Green",
            Trend = "Stable",
            ObservedAtUtc = DateTime.UtcNow
        };

    private static RoadSignalDto Signal(
        string id,
        string severity,
        double latitude,
        double longitude) =>
        new()
        {
            Id = id,
            SourceId = "sitg-roadworks",
            SourceName = "InfoMobilité Genève",
            Category = "Roadworks",
            Severity = severity,
            Title = "Travaux",
            Latitude = latitude,
            Longitude = longitude
        };

    private static RouteAdviceDto Route(
        string borderPointName,
        string direction,
        int decisionCost,
        int delayMinutes) =>
        new()
        {
            BorderPointName = borderPointName,
            Direction = direction,
            IsAvailable = true,
            DecisionCost = decisionCost,
            DelayMinutes = delayMinutes,
            DataCoveragePercent = 100
        };
}
