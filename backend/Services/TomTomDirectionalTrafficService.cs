using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrontiereLiveGe.Api.Dtos;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Services;

public sealed class TomTomDirectionalTrafficService : IDirectionalTrafficService
{
    private const int MaximumResponseBytes = 1_000_000;
    private readonly HttpClient _http;
    private readonly TomTomTrafficOptions _options;
    private readonly ILogger<TomTomDirectionalTrafficService> _logger;
    private readonly string _budgetStatePath;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly object _budgetLock = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public TomTomDirectionalTrafficService(
        IHttpClientFactory httpClientFactory,
        IOptions<TomTomTrafficOptions> options,
        IHostEnvironment environment,
        ILogger<TomTomDirectionalTrafficService> logger)
    {
        _http = httpClientFactory.CreateClient("TomTomTraffic");
        _options = options.Value;
        _logger = logger;

        var contentRoot = Path.GetFullPath(environment.ContentRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _budgetStatePath = Path.GetFullPath(_options.BudgetStatePath, contentRoot);
        var allowedPrefix = contentRoot + Path.DirectorySeparatorChar;
        if (!_budgetStatePath.StartsWith(
                allowedPrefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Traffic:TomTom:BudgetStatePath must stay inside the application content root.");
        }
    }

    public Task<IReadOnlyList<DirectionalTrafficDto>> GetCachedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var results = BorderCorridorCatalog.All
            .SelectMany(corridor => new[]
            {
                ReadCached(corridor.Name, "ToGeneva", "France → Genève"),
                ReadCached(corridor.Name, "ToFrance", "Genève → France")
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DirectionalTrafficDto>>(results);
    }

    public async Task<IReadOnlyList<DirectionalTrafficDto>> RefreshAsync(CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return BorderCorridorCatalog.All
                .SelectMany(c => new[]
                {
                    Unavailable(c.Name, "ToGeneva", "France → Genève", "Clé TomTom non configurée."),
                    Unavailable(c.Name, "ToFrance", "Genève → France", "Clé TomTom non configurée.")
                })
                .ToList();
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            var results = new List<DirectionalTrafficDto>(BorderCorridorCatalog.All.Count * 2);
            foreach (var corridor in BorderCorridorCatalog.All)
            {
                ct.ThrowIfCancellationRequested();
                results.Add(await GetDirectionAsync(
                    corridor.Name,
                    "ToGeneva",
                    "France → Genève",
                    corridor.France,
                    corridor.Crossing,
                    corridor.Geneva,
                    ct));
                results.Add(await GetDirectionAsync(
                    corridor.Name,
                    "ToFrance",
                    "Genève → France",
                    corridor.Geneva,
                    corridor.Crossing,
                    corridor.France,
                    ct));
            }

            return results;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public TrafficQuotaStatusDto GetQuotaStatus()
    {
        lock (_budgetLock)
        {
            var now = DateTime.UtcNow;
            var month = new DateOnly(now.Year, now.Month, 1);
            var limit = Math.Clamp(_options.MaxRequestsPerMonth, 14, 20000);
            var used = 0;
            var stateReadable = true;

            try
            {
                if (File.Exists(_budgetStatePath))
                {
                    var parts = File.ReadAllText(_budgetStatePath).Trim().Split('|', 2);
                    var storedMonth = default(DateOnly);
                    var storedCount = 0;
                    stateReadable = parts.Length == 2
                        && DateOnly.TryParseExact(
                            parts[0],
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out storedMonth)
                        && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out storedCount)
                        && storedCount >= 0;

                    if (stateReadable && storedMonth == month)
                    {
                        used = storedCount;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "TomTom budget state cannot be read.");
                stateReadable = false;
            }

            var percent = stateReadable ? Math.Min(100, (int)Math.Ceiling(used * 100d / limit)) : 100;
            var level = !stateReadable || percent >= _options.CriticalThresholdPercent
                ? "Critical"
                : percent >= _options.WarningThresholdPercent
                    ? "Warning"
                    : "Normal";
            var message = level switch
            {
                "Critical" => stateReadable
                    ? "Quota TomTom presque épuisé : les appels sont bloqués automatiquement à la limite."
                    : "Compteur TomTom illisible : les appels sont bloqués par sécurité.",
                "Warning" => "Quota TomTom à surveiller : le cache limite automatiquement les prochains appels.",
                _ => "Quota TomTom sous contrôle."
            };

            return new TrafficQuotaStatusDto
            {
                MonthUtc = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                RequestsUsed = used,
                MonthlyLimit = limit,
                RequestsRemaining = Math.Max(0, limit - used),
                UsagePercent = percent,
                Level = level,
                Message = message,
                ResetsAtUtc = month.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            };
        }
    }

    private async Task<DirectionalTrafficDto> GetDirectionAsync(
        string borderName,
        string direction,
        string label,
        BorderCorridorCatalog.Coordinate origin,
        BorderCorridorCatalog.Coordinate crossing,
        BorderCorridorCatalog.Coordinate destination,
        CancellationToken ct)
    {
        var cacheKey = $"{borderName}|{direction}";
        if (_cache.TryGetValue(cacheKey, out var cached)
            && DateTime.UtcNow - cached.StoredAtUtc < CacheDuration())
        {
            return cached.Value;
        }

        var locations = string.Create(
            CultureInfo.InvariantCulture,
            $"{origin.Latitude:F6},{origin.Longitude:F6}:" +
            $"{crossing.Latitude:F6},{crossing.Longitude:F6}:" +
            $"{destination.Latitude:F6},{destination.Longitude:F6}");
        var url =
            $"calculateRoute/{locations}/json?key={Uri.EscapeDataString(_options.ApiKey)}" +
            "&traffic=true&routeType=fastest&travelMode=car" +
            "&routeRepresentation=summaryOnly&computeTravelTimeFor=all";

        try
        {
            if (!TryReserveRequest())
            {
                _logger.LogWarning("TomTom monthly request budget reached; request blocked locally.");
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, "Plafond mensuel TomTom atteint ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, "Plafond mensuel TomTom atteint.");
            }

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TomTom route request failed for {Border}/{Direction}: HTTP {Status}.",
                    borderName,
                    direction,
                    (int)response.StatusCode);
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, $"TomTom a répondu HTTP {(int)response.StatusCode} ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, $"TomTom a répondu HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                throw new InvalidDataException("TomTom response exceeded the 1 MB safety limit.");
            }

            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<TomTomRoutesResponse>(stream, cancellationToken: ct);
            var summary = payload?.Routes.FirstOrDefault()?.Summary;
            if (summary is null || summary.TravelTimeInSeconds <= 0)
            {
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, "Aucun nouvel itinéraire TomTom ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, "Aucun itinéraire TomTom disponible.");
            }

            var duration = summary.TravelTimeInSeconds;
            var baseDuration = summary.NoTrafficTravelTimeInSeconds > 0
                ? summary.NoTrafficTravelTimeInSeconds
                : Math.Max(0, duration - summary.TrafficDelayInSeconds);
            var delaySeconds = summary.TrafficDelayInSeconds > 0
                ? summary.TrafficDelayInSeconds
                : Math.Max(0, duration - baseDuration);
            var delayMinutes = (int)Math.Ceiling(delaySeconds / 60d);
            var trend = "Stable";
            if (_cache.TryGetValue(cacheKey, out var previous) && previous.Value.DelayMinutes is int previousDelay)
            {
                trend = delayMinutes >= previousDelay + 2
                    ? "Rising"
                    : delayMinutes <= previousDelay - 2
                        ? "Falling"
                        : "Stable";
            }

            var now = DateTime.UtcNow;
            var result = new DirectionalTrafficDto
            {
                BorderPointName = borderName,
                Direction = direction,
                DirectionLabel = label,
                IsAvailable = true,
                TravelTimeMinutes = (int)Math.Ceiling(duration / 60d),
                FreeFlowTimeMinutes = (int)Math.Ceiling(baseDuration / 60d),
                DelayMinutes = delayMinutes,
                CongestionLevel = delayMinutes >= 15 ? "Red" : delayMinutes >= 7 ? "Orange" : "Green",
                Trend = trend,
                SourceName = "TomTom Traffic",
                ObservedAtUtc = now,
                IsStale = false,
                AgeMinutes = 0,
                ConfidencePercent = 85
            };

            _cache[cacheKey] = new CacheEntry(now, result);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            _logger.LogWarning(ex, "TomTom route request unavailable for {Border}/{Direction}.", borderName, direction);
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                return AsStale(cached, "TomTom est indisponible ; dernière mesure conservée.");
            }

            return Unavailable(borderName, direction, label, "Service TomTom temporairement indisponible.");
        }
    }

    private static DirectionalTrafficDto Unavailable(string border, string direction, string label, string reason) =>
        new()
        {
            BorderPointName = border,
            Direction = direction,
            DirectionLabel = label,
            IsAvailable = false,
            SourceName = "TomTom Traffic",
            ConfidencePercent = 0,
            Trend = "Unknown",
            UnavailableReason = reason
        };

    private DirectionalTrafficDto ReadCached(string border, string direction, string label)
    {
        var cacheKey = $"{border}|{direction}";
        if (!_cache.TryGetValue(cacheKey, out var cached))
        {
            return Unavailable(
                border,
                direction,
                label,
                _options.Enabled
                    ? "Première collecte TomTom en attente."
                    : "Clé TomTom non configurée.");
        }

        var age = DateTime.UtcNow - cached.StoredAtUtc;
        var freshness = CacheDuration();
        if (age > TimeSpan.FromTicks(freshness.Ticks * 4))
        {
            return Unavailable(border, direction, label, "Dernière mesure TomTom trop ancienne.");
        }

        return age <= freshness
            ? cached.Value
            : AsStale(cached, "Mesure TomTom en retard d’actualisation.");
    }

    private TimeSpan CacheDuration() =>
        TimeSpan.FromSeconds(Math.Clamp(_options.CacheSeconds, 1800, 3600));

    private static DirectionalTrafficDto AsStale(CacheEntry cached, string reason)
    {
        var value = cached.Value;
        return new DirectionalTrafficDto
        {
            BorderPointName = value.BorderPointName,
            Direction = value.Direction,
            DirectionLabel = value.DirectionLabel,
            IsAvailable = value.IsAvailable,
            TravelTimeMinutes = value.TravelTimeMinutes,
            FreeFlowTimeMinutes = value.FreeFlowTimeMinutes,
            DelayMinutes = value.DelayMinutes,
            CongestionLevel = value.CongestionLevel,
            Trend = value.Trend,
            SourceName = value.SourceName,
            ObservedAtUtc = value.ObservedAtUtc,
            IsStale = true,
            AgeMinutes = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - cached.StoredAtUtc).TotalMinutes)),
            ConfidencePercent = Math.Min(45, value.ConfidencePercent),
            UnavailableReason = reason
        };
    }

    private bool TryReserveRequest()
    {
        lock (_budgetLock)
        {
            try
            {
                var now = DateTime.UtcNow;
                var month = new DateOnly(now.Year, now.Month, 1);
                var requestsThisMonth = 0;

                if (File.Exists(_budgetStatePath))
                {
                    var parts = File.ReadAllText(_budgetStatePath).Trim().Split('|', 2);
                    if (parts.Length != 2
                        || !DateOnly.TryParseExact(
                            parts[0],
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var storedMonth)
                        || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var storedCount)
                        || storedCount < 0)
                    {
                        _logger.LogError("TomTom budget state is invalid; requests are blocked.");
                        return false;
                    }

                    if (storedMonth == month)
                    {
                        requestsThisMonth = storedCount;
                    }
                }

                var limit = Math.Clamp(_options.MaxRequestsPerMonth, 14, 20000);
                if (requestsThisMonth >= limit)
                {
                    return false;
                }

                var directory = Path.GetDirectoryName(_budgetStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    _budgetStatePath,
                    $"{month:yyyy-MM-dd}|{requestsThisMonth + 1}",
                    System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "TomTom budget state cannot be persisted; requests are blocked.");
                return false;
            }
        }
    }

    private sealed record CacheEntry(DateTime StoredAtUtc, DirectionalTrafficDto Value);

    internal sealed class TomTomRoutesResponse
    {
        [JsonPropertyName("routes")]
        public List<TomTomRoute> Routes { get; set; } = [];
    }

    internal sealed class TomTomRoute
    {
        [JsonPropertyName("summary")]
        public TomTomSummary? Summary { get; set; }
    }

    internal sealed class TomTomSummary
    {
        [JsonPropertyName("travelTimeInSeconds")]
        public int TravelTimeInSeconds { get; set; }

        [JsonPropertyName("trafficDelayInSeconds")]
        public int TrafficDelayInSeconds { get; set; }

        [JsonPropertyName("noTrafficTravelTimeInSeconds")]
        public int NoTrafficTravelTimeInSeconds { get; set; }
    }
}
