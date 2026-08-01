using System.Text.Json;
using FrontiereLiveGe.Api.Services.PublicData;

namespace FrontiereLiveGe.Api.Tests;

public sealed class GenevaRoadworksProviderTests
{
    private static readonly DateTime CheckedAtUtc =
        new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_KeepsActiveAndNearTermRoadworksAndRejectsUnsafeLinks()
    {
        const string geoJson =
            """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "geometry": { "type": "Point", "coordinates": [6.1279, 46.1406] },
                  "properties": {
                    "objectid": 11,
                    "date_debut": "20260701",
                    "date_fin": "20260801",
                    "adresse": " Route   de Bardonnex ",
                    "perturbation": "Fermeture complète de la route",
                    "moa": "État de Genève",
                    "fiche_info": "https://www.ge.ch/document/chantier-11"
                  }
                },
                {
                  "type": "Feature",
                  "geometry": { "type": "Point", "coordinates": [6.2156, 46.1935] },
                  "properties": {
                    "objectid": 12,
                    "date_debut": "20260804",
                    "date_fin": "20260810",
                    "adresse": "Thônex",
                    "perturbation": "Travaux préparatoires",
                    "moa": "",
                    "fiche_info": "https://evil.example/phishing"
                  }
                },
                {
                  "type": "Feature",
                  "geometry": { "type": "Point", "coordinates": [6.1, 46.2] },
                  "properties": {
                    "objectid": 13,
                    "date_debut": "20260810",
                    "date_fin": "20260820",
                    "adresse": "Trop loin dans le futur",
                    "perturbation": "Travaux",
                    "moa": "",
                    "fiche_info": ""
                  }
                },
                {
                  "type": "Feature",
                  "geometry": { "type": "Point", "coordinates": [6.1, 46.2] },
                  "properties": {
                    "objectid": 14,
                    "date_debut": "20260101",
                    "date_fin": "20260729",
                    "adresse": "Terminé",
                    "perturbation": "Travaux",
                    "moa": "",
                    "fiche_info": ""
                  }
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(geoJson);

        var result = GenevaRoadworksProvider.Parse(document.RootElement, CheckedAtUtc);

        Assert.Equal(4, result.RecordsCount);
        Assert.Equal(2, result.Signals.Count);

        var active = Assert.Single(result.Signals, signal => signal.Id == "sitg:11");
        Assert.Equal("Roadworks", active.Category);
        Assert.Equal("Critical", active.Severity);
        Assert.Equal("Route de Bardonnex", active.Title);
        Assert.Equal("https://www.ge.ch/document/chantier-11", active.DetailsUrl);
        Assert.Equal(DateTimeKind.Utc, active.StartsAtUtc!.Value.Kind);

        var upcoming = Assert.Single(result.Signals, signal => signal.Id == "sitg:12");
        Assert.Equal("UpcomingRoadworks", upcoming.Category);
        Assert.Equal("Info", upcoming.Severity);
        Assert.Null(upcoming.DetailsUrl);
    }

    [Fact]
    public void Parse_RejectsPayloadWithoutFeatureArray()
    {
        using var document = JsonDocument.Parse("""{"type":"FeatureCollection"}""");

        var exception = Assert.Throws<InvalidDataException>(
            () => GenevaRoadworksProvider.Parse(document.RootElement, CheckedAtUtc));

        Assert.Contains("features", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_AcceptsUppercaseMapServerProperties()
    {
        const string geoJson =
            """
            {
              "type": "FeatureCollection",
              "features": [{
                "type": "Feature",
                "geometry": { "type": "Point", "coordinates": [6.1279, 46.1406] },
                "properties": {
                  "OBJECTID": 21,
                  "DATE_DEBUT": "20260701",
                  "DATE_FIN": "20260801",
                  "ADRESSE": "Route de Bardonnex",
                  "PERTURBATION": "Circulation alternée",
                  "MOA": "État de Genève",
                  "FICHE_INFO": null
                }
              }]
            }
            """;

        using var document = JsonDocument.Parse(geoJson);

        var result = GenevaRoadworksProvider.Parse(document.RootElement, CheckedAtUtc);

        var signal = Assert.Single(result.Signals);
        Assert.Equal("sitg:21", signal.Id);
        Assert.Equal("Route de Bardonnex", signal.Title);
        Assert.Equal("Warning", signal.Severity);
    }
}
