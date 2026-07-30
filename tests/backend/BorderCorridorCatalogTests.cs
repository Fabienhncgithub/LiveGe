using FrontiereLiveGe.Api.Services;

namespace FrontiereLiveGe.Api.Tests;

public sealed class BorderCorridorCatalogTests
{
    [Fact]
    public void Catalog_ContainsSevenUniqueCrossings()
    {
        Assert.Equal(7, BorderCorridorCatalog.All.Count);
        Assert.Equal(
            BorderCorridorCatalog.All.Count,
            BorderCorridorCatalog.All.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var corridor = BorderCorridorCatalog.Find("bArDoNnEx");

        Assert.NotNull(corridor);
        Assert.Equal("Bardonnex", corridor.Name);
    }

    [Fact]
    public void DistanceToApproach_IsZeroAtCrossingAndLargeForRemotePoint()
    {
        var corridor = Assert.IsType<BorderCorridorCatalog.BorderCorridor>(
            BorderCorridorCatalog.Find("Bardonnex"));

        var atCrossing = BorderCorridorCatalog.DistanceToApproachKm(
            corridor,
            corridor.Crossing.Latitude,
            corridor.Crossing.Longitude);
        var inLausanne = BorderCorridorCatalog.DistanceToApproachKm(corridor, 46.5197, 6.6323);

        Assert.InRange(atCrossing, 0, 0.001);
        Assert.True(inLausanne > 20);
    }
}
