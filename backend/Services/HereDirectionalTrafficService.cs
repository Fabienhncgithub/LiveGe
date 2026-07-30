using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Services;

public sealed class HereDirectionalTrafficService : IDirectionalTrafficService
{
    private static readonly BorderCorridor[] Corridors =
    {
        new("Bardonnex", new(46.1248, 6.1207), new(46.1564, 6.1349)),
        new("Perly", new(46.0929, 6.0643), new(46.1237, 6.0867)),
        new("Moillesulaz", new(46.1805, 6.2228), new(46.1978, 6.1905)),
        new("Thônex-Vallard", new(46.1842, 6.2340), new(46.2014, 6.1980)),
        new("Anières", new(46.2837, 6.2363), new(46.2632, 6.2110)),
        new("Meyrin", new(46.2459, 6.0642), new(46.2263, 6.0974)),
        new("Ferney-Voltaire", new(46.2663, 6.1047), new(46.2397, 6.1200))
    };

    private readonly HttpClient _http;
    private readonly HereTrafficOptions _options;
    private readonly ILogger<HereDirectionalTrafficService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly object _budgetLock = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public HereDirectionalTrafficService(
        IHttpClientFactory httpClientFactory,
        IOptions<HereTrafficOptions> options,
        ILogger<HereDirectionalTrafficService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _http = httpClientFactory.CreateClient("HereTraffic");
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<DirectionalTrafficDto>> GetCurrentAsync(CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Corridors
                .SelectMany(c => new[]
                {
                    Unavailable(c.Name, "ToGeneva", "France → Genève", "Clé HERE non configurée."),
                    Unavailable(c.Name, "ToFrance", "Genève → France", "Clé HERE non configurée.")
                })
                .ToList();
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            var results = new List<DirectionalTrafficDto>(Corridors.Length * 2);
            foreach (var corridor in Corridors)
            {
                ct.ThrowIfCancellationRequested();
                results.Add(await GetDirectionAsync(
                    corridor.Name,
                    "ToGeneva",
                    "France → Genève",
                    corridor.France,
                    corridor.Geneva,
                    ct));
                results.Add(await GetDirectionAsync(
                    corridor.Name,
                    "ToFrance",
                    "Genève → France",
                    corridor.Geneva,
                    corridor.France,
                    ct));
            }

            await PersistReadingsAsync(results, ct);
            return results;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public HereQuotaStatusDto GetQuotaStatus()
    {
        lock (_budgetLock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var limit = Math.Clamp(_options.MaxRequestsPerDay, 14, 700);
            var used = 0;
            var stateReadable = true;

            try
            {
                var statePath = Path.GetFullPath(_options.BudgetStatePath);
                if (File.Exists(statePath))
                {
                    var parts = File.ReadAllText(statePath).Trim().Split('|', 2);
                    var storedDate = default(DateOnly);
                    var storedCount = 0;
                    stateReadable = parts.Length == 2
                        && DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out storedDate)
                        && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out storedCount)
                        && storedCount >= 0;

                    if (stateReadable && storedDate == today)
                    {
                        used = storedCount;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "HERE budget state cannot be read.");
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
                    ? "Quota HERE presque épuisé : les appels seront bloqués automatiquement à la limite."
                    : "Compteur HERE illisible : les appels sont bloqués par sécurité.",
                "Warning" => "Quota HERE à surveiller : le cache limite automatiquement les prochains appels.",
                _ => "Quota HERE sous contrôle."
            };

            return new HereQuotaStatusDto
            {
                DateUtc = today,
                RequestsUsed = used,
                DailyLimit = limit,
                RequestsRemaining = Math.Max(0, limit - used),
                UsagePercent = percent,
                Level = level,
                Message = message,
                ResetsAtUtc = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            };
        }
    }

    private async Task<DirectionalTrafficDto> GetDirectionAsync(
        string borderName,
        string direction,
        string label,
        Coordinate origin,
        Coordinate destination,
        CancellationToken ct)
    {
        var cacheKey = $"{borderName}|{direction}";
        if (_cache.TryGetValue(cacheKey, out var cached)
            && DateTime.UtcNow - cached.StoredAtUtc < TimeSpan.FromSeconds(Math.Clamp(_options.CacheSeconds, 1800, 3600)))
        {
            return cached.Value;
        }

        var coordinates = string.Create(CultureInfo.InvariantCulture,
            $"routes?transportMode=car&origin={origin.Latitude:F6},{origin.Longitude:F6}" +
            $"&destination={destination.Latitude:F6},{destination.Longitude:F6}&return=summary");
        var url = $"{coordinates}&apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            if (!TryReserveRequest())
            {
                _logger.LogWarning("HERE daily request budget reached; request blocked locally.");
                return _cache.TryGetValue(cacheKey, out cached)
                    ? cached.Value
                    : Unavailable(borderName, direction, label, "Plafond quotidien HERE atteint.");
            }

            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HERE route request failed for {Border}/{Direction}: HTTP {Status}.",
                    borderName, direction, (int)response.StatusCode);
                return Unavailable(borderName, direction, label, $"HERE a répondu HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<HereRoutesResponse>(cancellationToken: ct);
            var summary = payload?.Routes.FirstOrDefault()?.Sections.FirstOrDefault()?.Summary;
            if (summary is null)
            {
                return Unavailable(borderName, direction, label, "Aucun itinéraire HERE disponible.");
            }

            var duration = Math.Max(0, summary.Duration);
            var baseDuration = Math.Max(0, summary.BaseDuration);
            var delaySeconds = Math.Max(0, duration - baseDuration);
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
                SourceName = "HERE Traffic",
                ObservedAtUtc = DateTime.UtcNow,
                ConfidencePercent = 85
            };

            _cache[cacheKey] = new CacheEntry(DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "HERE route request unavailable for {Border}/{Direction}.", borderName, direction);
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                return cached.Value;
            }

            return Unavailable(borderName, direction, label, "Service HERE temporairement indisponible.");
        }
    }

    private static DirectionalTrafficDto Unavailable(string border, string direction, string label, string reason) =>
        new()
        {
            BorderPointName = border,
            Direction = direction,
            DirectionLabel = label,
            IsAvailable = false,
            SourceName = "HERE Traffic",
            ConfidencePercent = 0,
            Trend = "Unknown",
            UnavailableReason = reason
        };

    private async Task PersistReadingsAsync(IReadOnlyCollection<DirectionalTrafficDto> readings, CancellationToken ct)
    {
        var available = readings.Where(x => x.IsAvailable && x.ObservedAtUtc.HasValue).ToList();
        if (available.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var points = await db.BorderPoints.AsNoTracking().ToDictionaryAsync(x => x.Name, ct);

        foreach (var reading in available)
        {
            if (!points.TryGetValue(reading.BorderPointName, out var point))
            {
                continue;
            }

            var sourceName = $"HERE:{reading.Direction}";
            var observedAt = reading.ObservedAtUtc!.Value;
            var alreadyStored = await db.TrafficSnapshots
                .AsNoTracking()
                .AnyAsync(x => x.BorderPointId == point.Id
                    && x.SourceName == sourceName
                    && x.RecordedAtUtc == observedAt, ct);
            if (alreadyStored)
            {
                continue;
            }

            var congestion = Enum.TryParse<CongestionLevel>(reading.CongestionLevel, out var parsed)
                ? parsed
                : CongestionLevel.Green;
            db.TrafficSnapshots.Add(new TrafficSnapshot
            {
                BorderPointId = point.Id,
                RecordedAtUtc = observedAt,
                EstimatedDelayMinutes = reading.DelayMinutes ?? 0,
                SpeedKmh = 0,
                CongestionLevel = congestion,
                SourceName = sourceName
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private bool TryReserveRequest()
    {
        lock (_budgetLock)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var statePath = Path.GetFullPath(_options.BudgetStatePath);
                var requestsToday = 0;

                if (File.Exists(statePath))
                {
                    var parts = File.ReadAllText(statePath).Trim().Split('|', 2);
                    if (parts.Length != 2
                        || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var storedDate)
                        || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var storedCount)
                        || storedCount < 0)
                    {
                        _logger.LogError("HERE budget state is invalid; requests are blocked.");
                        return false;
                    }

                    if (storedDate == today)
                    {
                        requestsToday = storedCount;
                    }
                }

                var limit = Math.Clamp(_options.MaxRequestsPerDay, 14, 700);
                if (requestsToday >= limit)
                {
                    return false;
                }

                var directory = Path.GetDirectoryName(statePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    statePath,
                    $"{today:yyyy-MM-dd}|{requestsToday + 1}",
                    System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "HERE budget state cannot be persisted; requests are blocked.");
                return false;
            }
        }
    }

    private sealed record Coordinate(double Latitude, double Longitude);
    private sealed record BorderCorridor(string Name, Coordinate France, Coordinate Geneva);
    private sealed record CacheEntry(DateTime StoredAtUtc, DirectionalTrafficDto Value);

    private sealed class HereRoutesResponse
    {
        [JsonPropertyName("routes")]
        public List<HereRoute> Routes { get; set; } = new();
    }

    private sealed class HereRoute
    {
        [JsonPropertyName("sections")]
        public List<HereSection> Sections { get; set; } = new();
    }

    private sealed class HereSection
    {
        [JsonPropertyName("summary")]
        public HereSummary? Summary { get; set; }
    }

    private sealed class HereSummary
    {
        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("baseDuration")]
        public int BaseDuration { get; set; }
    }
}
