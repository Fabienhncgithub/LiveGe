using FrontiereLiveGe.Api.Services.PublicData;

namespace FrontiereLiveGe.Api.Tests;

public sealed class MeteoSwissProviderTests
{
    private static readonly DateTime CheckedAtUtc =
        new(2026, 7, 30, 10, 10, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_UsesLatestMeasurementAndCreatesGlobalCriticalSignal()
    {
        const string csv =
            """
            station_abbr;reference_timestamp;tre200s0;rre150z0;fu3010z1;htoauts0
            GVE;30.07.2026 09:50;20.1;0;18;0
            GVE;30.07.2026 10:00;-0.5;0.4;42;0
            """;

        var result = MeteoSwissProvider.Parse(csv, CheckedAtUtc);

        Assert.Equal(2, result.RecordsCount);
        Assert.Equal(new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc), result.ObservedAtUtc);
        Assert.Contains("-0", result.Summary);
        Assert.Contains("°C", result.Summary);
        Assert.Contains("0", result.Summary);
        Assert.Contains("mm", result.Summary);

        var signal = Assert.IsType<FrontiereLiveGe.Api.Dtos.RoadSignalDto>(result.Signal);
        Assert.Equal("Critical", signal.Severity);
        Assert.Equal("Weather", signal.Category);
        Assert.True(signal.AppliesToAllRoutes);
        Assert.Equal(result.ObservedAtUtc, signal.ObservedAtUtc);
    }

    [Fact]
    public void Parse_DoesNotCreateSignalForCalmDryConditions()
    {
        const string csv =
            """
            station_abbr;reference_timestamp;tre200s0;rre150z0;fu3010z1;htoauts0
            GVE;30.07.2026 10:00;22.4;0;12;0
            """;

        var result = MeteoSwissProvider.Parse(csv, CheckedAtUtc);

        Assert.Null(result.Signal);
        Assert.Contains("22", result.Summary);
        Assert.Contains("°C", result.Summary);
    }

    [Fact]
    public void Parse_RejectsMissingMeasurements()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => MeteoSwissProvider.Parse(
                "station_abbr;reference_timestamp",
                CheckedAtUtc));

        Assert.Contains("no measurements", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
