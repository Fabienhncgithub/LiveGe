using System.Globalization;
using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public sealed class MeteoSwissProvider : CachedPublicDataProvider
{
    private const string CurrentMeasurementsFile = "gve/ogd-smn_gve_t_now.csv";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MeteoSwissProvider> _logger;

    public MeteoSwissProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<MeteoSwissProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override TimeSpan CacheDuration => TimeSpan.FromMinutes(10);
    protected override TimeSpan MaximumStaleAge => TimeSpan.FromMinutes(45);
    protected override string SourceId => "meteoswiss-gve";
    protected override string SourceName => "MétéoSuisse — Cointrin";
    protected override string Coverage => "Mesures météo officielles à Genève/Cointrin, actualisées toutes les 10 minutes.";
    protected override string Attribution => "Source : MétéoSuisse";
    protected override string SourceUrl =>
        "https://www.meteosuisse.admin.ch/services-et-publications/service/open-data.html";
    protected override ILogger Logger => _logger;

    protected override async Task<PublicDataSnapshot> FetchFreshAsync(DateTime checkedAtUtc, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("MeteoSwiss");
        using var response = await client.GetAsync(
            CurrentMeasurementsFile,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 500_000)
        {
            throw new InvalidDataException("MétéoSuisse response exceeded the 500 KB safety limit.");
        }

        await response.Content.LoadIntoBufferAsync(500_000, ct);
        var csv = await response.Content.ReadAsStringAsync(ct);
        var parsed = Parse(csv, checkedAtUtc);
        if (parsed.ObservedAtUtc > checkedAtUtc.AddMinutes(10)
            || checkedAtUtc - parsed.ObservedAtUtc > MaximumStaleAge)
        {
            throw new InvalidDataException("MétéoSuisse returned an out-of-date measurement.");
        }

        return new PublicDataSnapshot
        {
            Source = OnlineStatus(
                checkedAtUtc,
                parsed.RecordsCount,
                parsed.Signal is null ? 0 : 1,
                parsed.ObservedAtUtc,
                parsed.Summary),
            Signals = parsed.Signal is null ? [] : [parsed.Signal]
        };
    }

    internal static ParseResult Parse(string csv, DateTime checkedAtUtc)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            throw new InvalidDataException("MétéoSuisse CSV contains no measurements.");
        }

        var headers = lines[0].TrimStart('\uFEFF').Split(';');
        var values = lines[^1].Split(';');
        var indexes = headers
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        string Value(string name)
        {
            return indexes.TryGetValue(name, out var index) && index < values.Length
                ? values[index]
                : string.Empty;
        }

        double? Number(string name) =>
            double.TryParse(Value(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;

        if (!DateTime.TryParseExact(
                Value("reference_timestamp"),
                "dd.MM.yyyy HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var observedUtc))
        {
            throw new InvalidDataException("MétéoSuisse CSV contains an invalid timestamp.");
        }

        // MétéoSuisse documents every reference timestamp in its ground-measurement
        // CSV files as UTC, even though the value itself has no explicit offset.
        var observedAtUtc = DateTime.SpecifyKind(observedUtc, DateTimeKind.Utc);
        var temperature = Number("tre200s0");
        var precipitation = Number("rre150z0");
        var gust = Number("fu3010z1");
        var snowDepth = Number("htoauts0");
        var severity = ClassifySeverity(temperature, precipitation, gust, snowDepth);
        var summary = BuildSummary(temperature, precipitation, gust, snowDepth);

        RoadSignalDto? signal = null;
        if (severity is not null)
        {
            signal = new RoadSignalDto
            {
                Id = $"meteoswiss:gve:{observedAtUtc:yyyyMMddHHmm}",
                SourceId = "meteoswiss-gve",
                SourceName = "MétéoSuisse — Cointrin",
                Category = "Weather",
                Severity = severity,
                Title = severity == "Critical"
                    ? "Conditions météo difficiles"
                    : "Météo susceptible de ralentir la circulation",
                Description = summary,
                Latitude = 46.247519,
                Longitude = 6.127742,
                AppliesToAllRoutes = true,
                ObservedAtUtc = observedAtUtc,
                DetailsUrl =
                    "https://www.meteosuisse.admin.ch/services-et-publications/applications/valeurs-mesurees-et-reseaux-de-mesure.html"
            };
        }

        return new ParseResult(lines.Length - 1, observedAtUtc, summary, signal);
    }

    private static string? ClassifySeverity(
        double? temperature,
        double? precipitation,
        double? gust,
        double? snowDepth)
    {
        if (precipitation is >= 3
            || gust is >= 80
            || (temperature is <= 0 && precipitation is >= 0.3))
        {
            return "Critical";
        }

        if (precipitation is >= 0.5 || gust is >= 50 || snowDepth is > 0)
        {
            return "Warning";
        }

        return precipitation is > 0 || gust is >= 35 ? "Info" : null;
    }

    private static string BuildSummary(
        double? temperature,
        double? precipitation,
        double? gust,
        double? snowDepth)
    {
        var parts = new List<string>();
        if (temperature.HasValue)
        {
            parts.Add($"{temperature.Value:0.#} °C");
        }

        if (precipitation.HasValue)
        {
            parts.Add($"{precipitation.Value:0.#} mm de précipitations sur 10 min");
        }

        if (gust.HasValue)
        {
            parts.Add($"rafales {gust.Value:0.#} km/h");
        }

        if (snowDepth is > 0)
        {
            parts.Add($"{snowDepth.Value:0} cm de neige au sol");
        }

        return parts.Count == 0 ? "Mesure reçue, paramètres routiers indisponibles." : string.Join(" · ", parts);
    }

    internal sealed record ParseResult(
        int RecordsCount,
        DateTime ObservedAtUtc,
        string Summary,
        RoadSignalDto? Signal);
}
