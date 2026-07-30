using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Extensions;
using FrontiereLiveGe.Api.Models;
using FrontiereLiveGe.Api.Security;
using FrontiereLiveGe.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace FrontiereLiveGe.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireRateLimiting("public");
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(AdminApiKeyDefaults.AuthorizationPolicy)
            .RequireRateLimiting("admin");

        group.MapGet("/border-points", async (AppDbContext db, CancellationToken ct) =>
        {
            var points = await db.BorderPoints
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            return Results.Ok(points.Select(x => x.ToDto()));
        });

        group.MapGet("/live", async (AppDbContext db, ITrendAnalyzer trendAnalyzer, CancellationToken ct) =>
        {
            var points = await db.BorderPoints
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var results = new List<LiveBorderStatusDto>();

            foreach (var point in points)
            {
                ct.ThrowIfCancellationRequested();

                var snapshot = await db.TrafficSnapshots
                    .AsNoTracking()
                    .Where(x => x.BorderPointId == point.Id
                        && x.SourceName == "HERE:ToGeneva")
                    .OrderByDescending(x => x.RecordedAtUtc)
                    .FirstOrDefaultAsync(ct);

                if (snapshot is null)
                {
                    // Do not manufacture a green/zero-delay reading when no real
                    // HERE snapshot exists for this crossing.
                    continue;
                }

                var trend = await trendAnalyzer.AnalyzeAsync(point.Id, snapshot.SourceName, ct);

                results.Add(new LiveBorderStatusDto
                {
                    BorderPointId = point.Id,
                    BorderPointName = point.Name,
                    EstimatedDelayMinutes = snapshot.EstimatedDelayMinutes,
                    SpeedKmh = snapshot.SpeedKmh,
                    CongestionLevel = snapshot.CongestionLevel,
                    Trend = trend.Trend,
                    PredictedDelayMinutes = trend.PredictedDelayMinutes,
                    PredictionLabel = trend.PredictionLabel,
                    RecordedAtUtc = snapshot.RecordedAtUtc
                });
            }

            return Results.Ok(results);
        });

        group.MapGet("/live/directions", async (IDirectionalTrafficService traffic, CancellationToken ct) =>
            Results.Ok(await traffic.GetCachedAsync(ct)));

        group.MapGet("/live/advice", async (IMobilityAdviceService advice, CancellationToken ct) =>
            Results.Ok(await advice.GetCurrentAsync(ct)));

        group.MapGet("/here/quota", (IDirectionalTrafficService traffic) =>
            Results.Ok(traffic.GetQuotaStatus()));

        group.MapGet("/here/history", async (AppDbContext db, CancellationToken ct) =>
        {
            var snapshots = await db.TrafficSnapshots
                .AsNoTracking()
                .Include(x => x.BorderPoint)
                .Where(x => x.SourceName.StartsWith("HERE:"))
                .OrderByDescending(x => x.RecordedAtUtc)
                .Take(300)
                .ToListAsync(ct);

            return Results.Ok(snapshots.Select(x =>
            {
                var direction = x.SourceName["HERE:".Length..];
                return new HereHistoryDto
                {
                    Id = x.Id,
                    BorderPointName = x.BorderPoint?.Name ?? "Inconnu",
                    Direction = direction,
                    DirectionLabel = direction == "ToGeneva" ? "France → Genève" : "Genève → France",
                    ObservedAtUtc = x.RecordedAtUtc,
                    DelayMinutes = x.EstimatedDelayMinutes,
                    CongestionLevel = x.CongestionLevel.ToString()
                };
            }));
        });

        group.MapGet("/here/forecast", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.TrafficSnapshots
                .AsNoTracking()
                .Where(x => x.SourceName.StartsWith("HERE:"))
                .OrderBy(x => x.RecordedAtUtc)
                .Select(x => new
                {
                    x.BorderPointId,
                    BorderPointName = x.BorderPoint != null ? x.BorderPoint.Name : "Inconnu",
                    x.SourceName,
                    x.RecordedAtUtc,
                    x.EstimatedDelayMinutes
                })
                .ToListAsync(ct);

            var localRows = rows.Select(x => new
            {
                x.BorderPointId,
                x.BorderPointName,
                x.SourceName,
                LocalTime = ToGenevaLocalTime(x.RecordedAtUtc),
                x.EstimatedDelayMinutes
            }).ToList();

            var daysCovered = localRows
                .Select(x => DateOnly.FromDateTime(x.LocalTime))
                .Distinct()
                .Count();
            const int minimumDays = 7;
            var response = new TrafficForecastDto
            {
                SamplesCount = rows.Count,
                DaysCovered = daysCovered,
                MinimumDaysRequired = minimumDays
            };

            if (daysCovered < minimumDays || rows.Count < 100)
            {
                response.Message =
                    $"Prévisions en apprentissage : {daysCovered}/{minimumDays} jours collectés et {rows.Count}/100 mesures.";
                return Results.Ok(response);
            }

            var dayNames = new Dictionary<DayOfWeek, string>
            {
                [DayOfWeek.Monday] = "lundi",
                [DayOfWeek.Tuesday] = "mardi",
                [DayOfWeek.Wednesday] = "mercredi",
                [DayOfWeek.Thursday] = "jeudi",
                [DayOfWeek.Friday] = "vendredi",
                [DayOfWeek.Saturday] = "samedi",
                [DayOfWeek.Sunday] = "dimanche"
            };

            foreach (var route in localRows
                         .Select(x => new
                         {
                             x.BorderPointId,
                             x.BorderPointName,
                             Direction = x.SourceName["HERE:".Length..]
                         })
                         .Distinct()
                         .OrderBy(x => x.BorderPointName)
                         .ThenBy(x => x.Direction))
            {
                var candidates = localRows
                    .Where(x => x.BorderPointId == route.BorderPointId
                        && x.SourceName == $"HERE:{route.Direction}")
                    .GroupBy(x => new
                    {
                        x.LocalTime.DayOfWeek,
                        TwoHourBucket = x.LocalTime.Hour / 2 * 2
                    })
                    .Select(group => new
                    {
                        group.Key.DayOfWeek,
                        Hour = group.Key.TwoHourBucket,
                        Average = group.Average(x => x.EstimatedDelayMinutes),
                        Samples = group.Count()
                    })
                    .Where(x => x.Samples >= 2)
                    .OrderBy(x => x.Average)
                    .FirstOrDefault();

                if (candidates is null)
                {
                    continue;
                }

                var confidence = Math.Min(85, 35 + daysCovered * 3 + candidates.Samples * 3);
                var label = route.Direction == "ToGeneva"
                    ? "France → Genève"
                    : "Genève → France";
                response.Suggestions.Add(new TrafficForecastSuggestionDto
                {
                    BorderPointId = route.BorderPointId,
                    BorderPointName = route.BorderPointName,
                    Direction = route.Direction,
                    DirectionLabel = label,
                    BestDay = dayNames[candidates.DayOfWeek],
                    BestHourStart = candidates.Hour,
                    AverageDelayMinutes = (int)Math.Round(candidates.Average),
                    SampleSize = candidates.Samples,
                    ConfidencePercent = confidence,
                    Advice =
                        $"À {route.BorderPointName}, pour le sens {label}, le créneau historiquement le plus fluide est {dayNames[candidates.DayOfWeek]} entre {candidates.Hour:00}h et {candidates.Hour + 2:00}h."
                });
            }

            response.IsAvailable = response.Suggestions.Count > 0;
            response.Message = response.IsAvailable
                ? "Prévisions calculées sur l’historique HERE local ; elles ne garantissent pas les conditions futures."
                : "Historique insuffisant pour produire une suggestion fiable.";
            return Results.Ok(response);
        });

        group.MapGet("/alerts", async (AppDbContext db, CancellationToken ct) =>
        {
            var alerts = await db.AlertEvents
                .AsNoTracking()
                .Include(x => x.BorderPoint)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);

            var dtos = alerts.Select(x => x.ToDto(x.BorderPoint?.Name ?? "Inconnu"));
            return Results.Ok(dtos);
        });

        group.MapGet("/history/{borderPointId:int}", async (int borderPointId, AppDbContext db, CancellationToken ct) =>
        {
            var snapshots = await db.TrafficSnapshots
                .AsNoTracking()
                .Where(x => x.BorderPointId == borderPointId)
                .OrderByDescending(x => x.RecordedAtUtc)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(snapshots.Select(x => x.ToDto()));
        });

        admin.MapGet("/settings", async (AppDbContext db, CancellationToken ct) =>
        {
            var settings = await db.BotSettings
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct)
                ?? new BotSettings
                {
                    PostingEnabled = false,
                    MinMinutesBetweenPosts = 60,
                    RisingThresholdMinutes = 10,
                    CriticalDelayMinutes = 30
                };

            return Results.Ok(settings.ToDto());
        });

        admin.MapPut("/settings", async (UpdateBotSettingsDto input, AppDbContext db, CancellationToken ct) =>
        {
            if (input.MinMinutesBetweenPosts < 1 || input.RisingThresholdMinutes < 0 || input.CriticalDelayMinutes < 1)
            {
                return Results.BadRequest(new { error = "Invalid settings values." });
            }

            var settings = await db.BotSettings
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (settings is null)
            {
                settings = new BotSettings();
                db.BotSettings.Add(settings);
            }

            settings.PostingEnabled = input.PostingEnabled;
            settings.MinMinutesBetweenPosts = input.MinMinutesBetweenPosts;
            settings.RisingThresholdMinutes = input.RisingThresholdMinutes;
            settings.CriticalDelayMinutes = input.CriticalDelayMinutes;

            await db.SaveChangesAsync(ct);

            return Results.Ok(settings.ToDto());
        });

        admin.MapPost("/run-once", async (IBorderRadarRunner runner, CancellationToken ct) =>
        {
            var result = await runner.RunAsync(ct);
            return Results.Ok(new RunSummaryDto
            {
                SnapshotsCreated = result.SnapshotsCreated,
                AlertsCreated = result.AlertsCreated,
                AlertsPosted = result.AlertsPosted,
                RanAtUtc = result.RanAtUtc
            });
        });

        admin.MapPost("/publish-test", async (AppDbContext db, IPostPublisher publisher, CancellationToken ct) =>
        {
            var point = await db.BorderPoints.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            var label = point?.Name ?? "Frontière";
            var message = $"🧪 Test Frontière Live GE — {label} — {DateTime.UtcNow:HH:mm} UTC";

            var alert = new AlertEvent
            {
                BorderPointId = point?.Id ?? 0,
                CreatedAtUtc = DateTime.UtcNow,
                Message = message,
                Severity = AlertSeverity.Info,
                Trend = TrendDirection.Stable,
                IsPosted = false,
                PostedAtUtc = null,
                Fingerprint = $"test|{DateTime.UtcNow:yyyyMMddHHmmss}",
                PredictedDelayMinutes = null
            };

            await publisher.PublishAsync(alert, message, ct);
            return Results.Ok(new { posted = publisher.IsLive, simulated = !publisher.IsLive, message });
        });

        admin.MapGet("/x/me", async (IHttpClientFactory httpClientFactory, IXTokenService tokenService, CancellationToken ct) =>
        {
            try
            {
                var token = await tokenService.GetAccessTokenAsync(ct);
                var client = httpClientFactory.CreateClient("XApi");

                async Task<(bool ok, string body, int status)> SendAsync(string accessToken)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "users/me");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using var response = await client.SendAsync(request, ct);
                    var body = await response.Content.ReadAsStringAsync(ct);
                    return (response.IsSuccessStatusCode, body, (int)response.StatusCode);
                }

                var (ok, body, status) = await SendAsync(token);
                if (!ok && status == 401)
                {
                    // Try a refresh once if the token is expired.
                    var refreshed = await tokenService.TryRefreshAsync(ct, force: true);
                    if (refreshed)
                    {
                        token = await tokenService.GetAccessTokenAsync(ct);
                        (ok, body, status) = await SendAsync(token);
                    }
                }

                if (!ok)
                {
                    return Results.Problem(
                        title: "X API error",
                        detail: "The X API request failed.",
                        statusCode: status);
                }

                return Results.Content(body, "application/json");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "X OAuth2 not configured",
                    detail: ex.Message,
                    statusCode: 400);
            }
        });

        return app;
    }

    private static DateTime ToGenevaLocalTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"));
        }
    }
}
