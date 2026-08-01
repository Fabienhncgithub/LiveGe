using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public sealed partial class GenevaRoadworksProvider : CachedPublicDataProvider
{
    private const string MapServerFallbackUrl =
        "https://vector.sitg.ge.ch/arcgis/rest/services/INFOMOB_CHANTIER_POINT/MapServer/0/";
    private const string Query =
        "query?where=1%3D1" +
        "&outFields=objectid,date_debut,date_fin,adresse,type,fiche_info,perturbation,impact_global,label_pcm,moa" +
        "&returnGeometry=true&outSR=4326&resultRecordCount=500&f=geojson";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GenevaRoadworksProvider> _logger;

    public GenevaRoadworksProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GenevaRoadworksProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override TimeSpan CacheDuration => TimeSpan.FromMinutes(30);
    protected override TimeSpan MaximumStaleAge => TimeSpan.FromHours(48);
    protected override string SourceId => "sitg-roadworks";
    protected override string SourceName => "InfoMobilité Genève";
    protected override string Coverage => "Chantiers importants annoncés par l’État de Genève, mise à jour quotidienne.";
    protected override string Attribution => "Source : SITG / État de Genève";
    protected override string SourceUrl => "https://sitg.ge.ch/donnees/pcmob-chantier-consult";
    protected override ILogger Logger => _logger;

    protected override async Task<PublicDataSnapshot> FetchFreshAsync(DateTime checkedAtUtc, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("GenevaRoadworks");
        try
        {
            return await FetchFromEndpointAsync(client, Query, checkedAtUtc, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "The SITG FeatureServer endpoint failed; trying the MapServer endpoint.");
            return await FetchFromEndpointAsync(
                client,
                $"{MapServerFallbackUrl}{Query}",
                checkedAtUtc,
                ct);
        }
    }

    private async Task<PublicDataSnapshot> FetchFromEndpointAsync(
        HttpClient client,
        string requestUrl,
        DateTime checkedAtUtc,
        CancellationToken ct)
    {
        using var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 2_000_000)
        {
            throw new InvalidDataException("SITG response exceeded the 2 MB safety limit.");
        }

        await response.Content.LoadIntoBufferAsync(2_000_000, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(
            stream,
            new JsonDocumentOptions { MaxDepth = 24 },
            ct);

        var parsed = Parse(document.RootElement, checkedAtUtc);
        return new PublicDataSnapshot
        {
            Source = OnlineStatus(
                checkedAtUtc,
                parsed.RecordsCount,
                parsed.Signals.Count,
                checkedAtUtc,
                parsed.Signals.Count == 0
                    ? "Aucun chantier actif ou imminent dans le flux."
                    : $"{parsed.Signals.Count} chantier(s) actif(s) ou imminent(s)."),
            Signals = parsed.Signals
        };
    }

    internal static ParseResult Parse(JsonElement root, DateTime checkedAtUtc)
    {
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("SITG GeoJSON does not contain a features array.");
        }

        var localToday = DateOnly.FromDateTime(GenevaTime.FromUtc(checkedAtUtc));
        var latestIncludedDate = localToday.AddDays(7);
        var signals = new List<RoadSignalDto>();
        var recordCount = 0;

        foreach (var feature in features.EnumerateArray())
        {
            recordCount++;
            if (!TryReadCoordinates(feature, out var longitude, out var latitude)
                || !feature.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var start = ParseCompactDate(GetString(properties, "date_debut"));
            var end = ParseCompactDate(GetString(properties, "date_fin"));
            if (end.HasValue && end.Value < localToday)
            {
                continue;
            }

            if (start.HasValue && start.Value > latestIncludedDate)
            {
                continue;
            }

            var address = Clean(GetString(properties, "adresse"), 120);
            var disruption = Clean(GetString(properties, "perturbation"), 480);
            if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(disruption))
            {
                continue;
            }

            var isUpcoming = start.HasValue && start.Value > localToday;
            var severity = isUpcoming ? "Info" : ClassifySeverity(disruption);
            var objectId = GetInt(properties, "objectid") ?? recordCount;
            var owner = Clean(GetString(properties, "moa"), 80);

            signals.Add(new RoadSignalDto
            {
                Id = $"sitg:{objectId}",
                SourceId = "sitg-roadworks",
                SourceName = "InfoMobilité Genève",
                Category = isUpcoming ? "UpcomingRoadworks" : "Roadworks",
                Severity = severity,
                Title = string.IsNullOrWhiteSpace(address)
                    ? "Chantier signalé à Genève"
                    : address,
                Description = string.IsNullOrWhiteSpace(owner)
                    ? disruption
                    : $"{disruption} — Maître d’ouvrage : {owner}",
                Latitude = latitude,
                Longitude = longitude,
                StartsAtUtc = start.HasValue
                    ? GenevaTime.ToUtc(start.Value.ToDateTime(TimeOnly.MinValue))
                    : null,
                EndsAtUtc = end.HasValue
                    ? GenevaTime.ToUtc(end.Value.AddDays(1).ToDateTime(TimeOnly.MinValue))
                    : null,
                ObservedAtUtc = checkedAtUtc,
                DetailsUrl = SafeGenevaUrl(GetString(properties, "fiche_info"))
            });
        }

        return new ParseResult(recordCount, signals);
    }

    private static bool TryReadCoordinates(
        JsonElement feature,
        out double longitude,
        out double latitude)
    {
        longitude = 0;
        latitude = 0;

        return feature.TryGetProperty("geometry", out var geometry)
            && geometry.ValueKind == JsonValueKind.Object
            && geometry.TryGetProperty("coordinates", out var coordinates)
            && coordinates.ValueKind == JsonValueKind.Array
            && coordinates.GetArrayLength() >= 2
            && coordinates[0].TryGetDouble(out longitude)
            && coordinates[1].TryGetDouble(out latitude)
            && latitude is >= 45.7 and <= 46.6
            && longitude is >= 5.7 and <= 6.7;
    }

    private static string ClassifySeverity(string text)
    {
        var describesFullClosure = ContainsAny(text,
                "fermeture de la route",
                "fermeture des routes",
                "fermeture complète",
                "fermée à la circulation",
                "fermé à la circulation");
        var isLimitedClosure = ContainsAny(text,
            "de nuit",
            "ponctuelle",
            "ponctuelles",
            "par tronçon",
            "par étape",
            "en fin de chantier");

        if (describesFullClosure && !isLimitedClosure)
        {
            return "Critical";
        }

        return ContainsAny(text,
            "fermeture",
            "suppression de voie",
            "suppression des voies",
            "circulation alternée",
            "mise en sens unique",
            "déviation",
            "ralentissement")
            ? "Warning"
            : "Info";
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static DateOnly? ParseCompactDate(string value) =>
        DateOnly.TryParseExact(
            value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static string GetString(JsonElement properties, string name)
    {
        if (!TryGetProperty(properties, name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
    }

    private static int? GetInt(JsonElement properties, string name)
    {
        if (!TryGetProperty(properties, name, out var value))
        {
            return null;
        }

        return value.TryGetInt32(out var number) ? number : null;
    }

    private static bool TryGetProperty(JsonElement properties, string name, out JsonElement value)
    {
        if (properties.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string Clean(string value, int maxLength)
    {
        var clean = WhitespaceRegex().Replace(value.Trim(), " ");
        return clean.Length <= maxLength ? clean : $"{clean[..(maxLength - 1)]}…";
    }

    private static string? SafeGenevaUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("ge.ch", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.EndsWith(".ge.ch", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return uri.ToString();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    internal sealed record ParseResult(int RecordsCount, IReadOnlyList<RoadSignalDto> Signals);
}
