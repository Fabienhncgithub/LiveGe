using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Extensions;

public static class DtoMappingExtensions
{
    public static BorderPointDto ToDto(this BorderPoint borderPoint)
    {
        return new BorderPointDto
        {
            Id = borderPoint.Id,
            Name = borderPoint.Name,
            Latitude = borderPoint.Latitude,
            Longitude = borderPoint.Longitude,
            IsActive = borderPoint.IsActive
        };
    }

    public static BotSettingsDto ToDto(this BotSettings settings)
    {
        return new BotSettingsDto
        {
            PostingEnabled = settings.PostingEnabled,
            MinMinutesBetweenPosts = settings.MinMinutesBetweenPosts,
            RisingThresholdMinutes = settings.RisingThresholdMinutes,
            CriticalDelayMinutes = settings.CriticalDelayMinutes
        };
    }

    public static TrafficSnapshotDto ToDto(this TrafficSnapshot snapshot)
    {
        return new TrafficSnapshotDto
        {
            Id = snapshot.Id,
            BorderPointId = snapshot.BorderPointId,
            RecordedAtUtc = snapshot.RecordedAtUtc,
            EstimatedDelayMinutes = snapshot.EstimatedDelayMinutes,
            SpeedKmh = snapshot.SpeedKmh,
            CongestionLevel = snapshot.CongestionLevel,
            SourceName = snapshot.SourceName
        };
    }

    public static AlertEventDto ToDto(this AlertEvent alert, string borderPointName)
    {
        return new AlertEventDto
        {
            Id = alert.Id,
            BorderPointId = alert.BorderPointId,
            BorderPointName = borderPointName,
            CreatedAtUtc = alert.CreatedAtUtc,
            Message = alert.Message,
            Severity = alert.Severity,
            Trend = alert.Trend,
            IsPosted = alert.IsPosted,
            PostedAtUtc = alert.PostedAtUtc,
            PredictedDelayMinutes = alert.PredictedDelayMinutes
        };
    }
}
