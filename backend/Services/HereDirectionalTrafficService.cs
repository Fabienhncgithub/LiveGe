using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FrontiereLiveGe.Api.Dtos;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Services;

public sealed class HereDirectionalTrafficService : IDirectionalTrafficService
{
    private readonly HttpClient _http;
    private readonly HereTrafficOptions _options;
    private readonly ILogger<HereDirectionalTrafficService> _logger;
    private readonly string _budgetStatePath;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly object _budgetLock = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public HereDirectionalTrafficService(
        IHttpClientFactory httpClientFactory,
        IOptions<HereTrafficOptions> options,
        IHostEnvironment environment,
        ILogger<HereDirectionalTrafficService> logger)
    {
        _http = httpClientFactory.CreateClient("HereTraffic");
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
                "Traffic:Here:BudgetStatePath must stay inside the application content root.");
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
                    Unavailable(c.Name, "ToGeneva", "France → Genève", "Clé HERE non configurée."),
                    Unavailable(c.Name, "ToFrance", "Genève → France", "Clé HERE non configurée.")
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

    public HereQuotaStatusDto GetQuotaStatus()
    {
        lock (_budgetLock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var limit = Math.Clamp(_options.MaxRequestsPerDay, 14, 600);
            var used = 0;
            var stateReadable = true;

            try
            {
                var statePath = _budgetStatePath;
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
        BorderCorridorCatalog.Coordinate origin,
        BorderCorridorCatalog.Coordinate crossing,
        BorderCorridorCatalog.Coordinate destination,
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
            $"&via={crossing.Latitude:F6},{crossing.Longitude:F6}" +
            $"&destination={destination.Latitude:F6},{destination.Longitude:F6}&return=summary");
        var url = $"{coordinates}&apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            if (!TryReserveRequest())
            {
                _logger.LogWarning("HERE daily request budget reached; request blocked locally.");
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, "Plafond quotidien HERE atteint ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, "Plafond quotidien HERE atteint.");
            }

            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HERE route request failed for {Border}/{Direction}: HTTP {Status}.",
                    borderName, direction, (int)response.StatusCode);
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, $"HERE a répondu HTTP {(int)response.StatusCode} ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, $"HERE a répondu HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<HereRoutesResponse>(cancellationToken: ct);
            var summaries = payload?.Routes.FirstOrDefault()?.Sections
                .Select(x => x.Summary)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();
            if (summaries is null || summaries.Count == 0)
            {
                return _cache.TryGetValue(cacheKey, out cached)
                    ? AsStale(cached, "Aucun nouvel itinéraire HERE ; dernière mesure conservée.")
                    : Unavailable(borderName, direction, label, "Aucun itinéraire HERE disponible.");
            }

            var duration = summaries.Sum(x => Math.Max(0, x.Duration));
            var baseDuration = summaries.Sum(x => Math.Max(0, x.BaseDuration));
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
                IsStale = false,
                AgeMinutes = 0,
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
                return AsStale(cached, "HERE est indisponible ; dernière mesure conservée.");
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
                    ? "Première collecte HERE en attente."
                    : "Clé HERE non configurée.");
        }

        var age = DateTime.UtcNow - cached.StoredAtUtc;
        var freshness = TimeSpan.FromSeconds(Math.Clamp(_options.CacheSeconds, 1800, 3600));
        if (age > TimeSpan.FromTicks(freshness.Ticks * 4))
        {
            return Unavailable(border, direction, label, "Dernière mesure HERE trop ancienne.");
        }

        return age <= freshness
            ? cached.Value
            : AsStale(cached, "Mesure HERE en retard d’actualisation.");
    }

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
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var statePath = _budgetStatePath;
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

                var limit = Math.Clamp(_options.MaxRequestsPerDay, 14, 600);
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
