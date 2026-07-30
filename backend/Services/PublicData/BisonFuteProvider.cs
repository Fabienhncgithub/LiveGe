using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public sealed partial class BisonFuteProvider : CachedPublicDataProvider
{
    private const string FeedFile = "content.xml";
    private const int MaximumResponseBytes = 8_000_000;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BisonFuteProvider> _logger;

    public BisonFuteProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<BisonFuteProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override TimeSpan CacheDuration => TimeSpan.FromMinutes(10);
    protected override TimeSpan MaximumStaleAge => TimeSpan.FromHours(1);
    protected override string SourceId => "bison-fute-open";
    protected override string SourceName => "Bison Futé — DIR";
    protected override string Coverage =>
        "Réseau routier national français non concédé. A40/A41 et de nombreuses routes frontalières peuvent être absentes.";
    protected override string Attribution => "Source : Bison Futé / transport.data.gouv.fr — Licence Ouverte 2.0";
    protected override string SourceUrl =>
        "https://transport.data.gouv.fr/datasets/evenements-routiers-sur-le-reseau-routier-national-non-concede";
    protected override ILogger Logger => _logger;

    protected override async Task<PublicDataSnapshot> FetchFreshAsync(DateTime checkedAtUtc, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("BisonFute");
        using var response = await client.GetAsync(FeedFile, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException("Bison Futé response exceeded the 8 MB safety limit.");
        }

        await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = MaximumResponseBytes
        };
        using var reader = XmlReader.Create(stream, settings);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
        var parsed = Parse(document, checkedAtUtc);

        return new PublicDataSnapshot
        {
            Source = OnlineStatus(
                checkedAtUtc,
                parsed.RecordsCount,
                parsed.Signals.Count,
                parsed.PublicationTimeUtc,
                parsed.Signals.Count == 0
                    ? "Aucun événement du flux ouvert DIR ne se trouve près du Grand Genève."
                    : $"{parsed.Signals.Count} événement(s) français pertinent(s) dans la zone élargie."),
            Signals = parsed.Signals
        };
    }

    internal static ParseResult Parse(XDocument document, DateTime checkedAtUtc)
    {
        var publicationTime = ParseDate(
            document.Descendants().FirstOrDefault(x => x.Name.LocalName == "publicationTime")?.Value);
        var recordsCount = 0;
        var signals = new List<RoadSignalDto>();
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        foreach (var situation in document.Descendants().Where(x => x.Name.LocalName == "situation"))
        {
            var situationSeverity = situation.Elements()
                .FirstOrDefault(x => x.Name.LocalName == "overallSeverity")?.Value;
            var situationId = situation.Attribute("id")?.Value ?? "unknown";

            foreach (var record in situation.Elements().Where(x => x.Name.LocalName == "situationRecord"))
            {
                recordsCount++;
                var start = ParseDate(record.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "overallStartTime")?.Value);
                var end = ParseDate(record.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "overallEndTime")?.Value);
                if (end.HasValue && end.Value < checkedAtUtc)
                {
                    continue;
                }

                var isUpcoming = start.HasValue && start.Value > checkedAtUtc;
                if (start.HasValue && start.Value > checkedAtUtc.AddHours(24))
                {
                    continue;
                }

                var coordinates = record.Descendants()
                    .Where(x => x.Name.LocalName == "pointCoordinates")
                    .Select(ReadCoordinate)
                    .Where(x => x is not null)
                    .Select(x => x!.Value)
                    .ToList();
                if (coordinates.Count == 0)
                {
                    continue;
                }

                var latitude = coordinates.Average(x => x.Latitude);
                var longitude = coordinates.Average(x => x.Longitude);
                if (latitude is < 45.65 or > 46.60 || longitude is < 5.40 or > 6.80)
                {
                    continue;
                }

                var recordType = record.Attribute(xsi + "type")?.Value ?? "RoadEvent";
                var eventType = NormalizeType(recordType);
                var recordId = record.Attribute("id")?.Value ?? recordsCount.ToString(CultureInfo.InvariantCulture);
                var roadNumber = Clean(record.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "roadNumber")?.Value ?? string.Empty, 30);
                var description = FindComment(record, "description");
                var location = FindComment(record, "locationDescriptor");
                var severity = isUpcoming
                    ? "Info"
                    : ClassifySeverity(situationSeverity, recordType, description);
                var observedAt = ParseDate(record.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "situationRecordObservationTime")?.Value)
                    ?? ParseDate(record.Descendants()
                        .FirstOrDefault(x => x.Name.LocalName == "situationRecordVersionTime")?.Value)
                    ?? publicationTime
                    ?? checkedAtUtc;
                var travelDirection = ParseTpegDirection(record.Descendants()
                        .FirstOrDefault(x => x.Name.LocalName == "tpegDirection")?.Value)
                    ?? ParseRelativeDirection(
                        record.Descendants()
                            .FirstOrDefault(x => x.Name.LocalName == "directionRelativeOnLinearSection")?.Value,
                        coordinates);

                var titleParts = new[] { roadNumber, location }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var titleSuffix = string.Join(" · ", titleParts);

                signals.Add(new RoadSignalDto
                {
                    Id = $"bison:{situationId}:{recordId}",
                    SourceId = "bison-fute-open",
                    SourceName = "Bison Futé — DIR",
                    Category = isUpcoming ? $"Upcoming{eventType}" : eventType,
                    Severity = severity,
                    Title = string.IsNullOrWhiteSpace(titleSuffix)
                        ? $"{(isUpcoming ? "À venir · " : string.Empty)}{HumanizeType(eventType)}"
                        : $"{(isUpcoming ? "À venir · " : string.Empty)}{HumanizeType(eventType)} · {titleSuffix}",
                    Description = string.IsNullOrWhiteSpace(description)
                        ? "Événement routier publié dans le flux DATEX II ouvert."
                        : description,
                    Latitude = latitude,
                    Longitude = longitude,
                    TravelDirectionDegrees = travelDirection,
                    StartsAtUtc = start,
                    EndsAtUtc = end,
                    ObservedAtUtc = observedAt,
                    DetailsUrl =
                        "https://transport.data.gouv.fr/datasets/evenements-routiers-sur-le-reseau-routier-national-non-concede"
                });
            }
        }

        return new ParseResult(recordsCount, publicationTime, signals);
    }

    private static (double Latitude, double Longitude)? ReadCoordinate(XElement element)
    {
        var latitudeText = element.Elements().FirstOrDefault(x => x.Name.LocalName == "latitude")?.Value;
        var longitudeText = element.Elements().FirstOrDefault(x => x.Name.LocalName == "longitude")?.Value;
        if (!double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return (latitude, longitude);
    }

    private static string FindComment(XElement record, string commentType)
    {
        foreach (var publicComment in record.Descendants()
                     .Where(x => x.Name.LocalName == "generalPublicComment"))
        {
            var type = publicComment.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "commentType")?.Value;
            if (!string.Equals(type, commentType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = publicComment.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "value")?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Clean(value, 480);
            }
        }

        return string.Empty;
    }

    private static string NormalizeType(string value)
    {
        if (value.Contains("Accident", StringComparison.OrdinalIgnoreCase)) return "Accident";
        if (value.Contains("AbnormalTraffic", StringComparison.OrdinalIgnoreCase)) return "Congestion";
        if (value.Contains("Roadworks", StringComparison.OrdinalIgnoreCase)) return "Roadworks";
        if (value.Contains("Weather", StringComparison.OrdinalIgnoreCase)) return "Weather";
        if (value.Contains("Obstruction", StringComparison.OrdinalIgnoreCase)) return "Hazard";
        if (value.Contains("RoadOrCarriageway", StringComparison.OrdinalIgnoreCase)) return "Restriction";
        return "Incident";
    }

    private static string HumanizeType(string value) => value switch
    {
        "Accident" => "Accident",
        "Congestion" => "Congestion",
        "Roadworks" => "Travaux",
        "Weather" => "Conditions météo",
        "Hazard" => "Danger sur la route",
        "Restriction" => "Restriction de circulation",
        _ => "Événement routier"
    };

    internal static double? ParseTpegDirection(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "northbound" => 0,
            "northeastbound" => 45,
            "eastbound" => 90,
            "southeastbound" => 135,
            "southbound" => 180,
            "southwestbound" => 225,
            "westbound" => 270,
            "northwestbound" => 315,
            _ => null
        };

    private static double? ParseRelativeDirection(
        string? value,
        IReadOnlyList<(double Latitude, double Longitude)> coordinates)
    {
        if (coordinates.Count < 2
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "both", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var first = coordinates[0];
        var last = coordinates[^1];
        if (Math.Abs(first.Latitude - last.Latitude) < 0.00001
            && Math.Abs(first.Longitude - last.Longitude) < 0.00001)
        {
            return null;
        }

        var bearing = BearingDegrees(first, last);
        return string.Equals(value, "opposite", StringComparison.OrdinalIgnoreCase)
            ? (bearing + 180) % 360
            : string.Equals(value, "aligned", StringComparison.OrdinalIgnoreCase)
                ? bearing
                : null;
    }

    private static double BearingDegrees(
        (double Latitude, double Longitude) start,
        (double Latitude, double Longitude) end)
    {
        var latitude1 = start.Latitude * Math.PI / 180;
        var latitude2 = end.Latitude * Math.PI / 180;
        var longitudeDelta = (end.Longitude - start.Longitude) * Math.PI / 180;
        var y = Math.Sin(longitudeDelta) * Math.Cos(latitude2);
        var x = Math.Cos(latitude1) * Math.Sin(latitude2)
            - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitudeDelta);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static string ClassifySeverity(string? severity, string recordType, string description)
    {
        if (ContainsAny(recordType, "Closure", "Blocked")
            || ContainsAny(description, "route fermée", "fermeture complète", "contresens"))
        {
            return "Critical";
        }

        if (ContainsAny(severity ?? string.Empty, "highest", "high", "severe"))
        {
            return "Critical";
        }

        return ContainsAny(severity ?? string.Empty, "medium") ? "Warning" : "Info";
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static DateTime? ParseDate(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed.UtcDateTime
            : null;

    private static string Clean(string value, int maxLength)
    {
        var clean = WhitespaceRegex().Replace(value.Trim(), " ");
        return clean.Length <= maxLength ? clean : $"{clean[..(maxLength - 1)]}…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    internal sealed record ParseResult(
        int RecordsCount,
        DateTime? PublicationTimeUtc,
        IReadOnlyList<RoadSignalDto> Signals);
}
