using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Extensions;
using FrontiereLiveGe.Api.Models;
using FrontiereLiveGe.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace FrontiereLiveGe.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

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
                    .Where(x => x.BorderPointId == point.Id)
                    .OrderByDescending(x => x.RecordedAtUtc)
                    .FirstOrDefaultAsync(ct);

                if (snapshot is null)
                {
                    results.Add(new LiveBorderStatusDto
                    {
                        BorderPointId = point.Id,
                        BorderPointName = point.Name,
                        EstimatedDelayMinutes = 0,
                        SpeedKmh = 0,
                        CongestionLevel = CongestionLevel.Green,
                        Trend = TrendDirection.Stable,
                        PredictedDelayMinutes = 0,
                        PredictionLabel = "stable",
                        RecordedAtUtc = DateTime.UtcNow
                    });

                    continue;
                }

                var trend = await trendAnalyzer.AnalyzeAsync(point.Id, ct);

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

        group.MapGet("/settings", async (AppDbContext db, CancellationToken ct) =>
        {
            var settings = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                ?? new BotSettings
                {
                    PostingEnabled = true,
                    MinMinutesBetweenPosts = 60,
                    RisingThresholdMinutes = 10,
                    CriticalDelayMinutes = 30
                };

            return Results.Ok(settings.ToDto());
        });

        group.MapPut("/settings", async (UpdateBotSettingsDto input, AppDbContext db, CancellationToken ct) =>
        {
            if (input.MinMinutesBetweenPosts < 1 || input.RisingThresholdMinutes < 0 || input.CriticalDelayMinutes < 1)
            {
                return Results.BadRequest(new { error = "Invalid settings values." });
            }

            var settings = await db.BotSettings.FirstOrDefaultAsync(ct);
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

        group.MapPost("/run-once", async (IBorderRadarRunner runner, CancellationToken ct) =>
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

        group.MapPost("/publish-test", async (AppDbContext db, IPostPublisher publisher, CancellationToken ct) =>
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
            return Results.Ok(new { posted = true, message });
        });

        group.MapGet("/x/me", async (IHttpClientFactory httpClientFactory, IXTokenService tokenService, CancellationToken ct) =>
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
                    var refreshed = await tokenService.TryRefreshAsync(ct);
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
                        detail: body,
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
}
