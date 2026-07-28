using FrontiereLiveGe.Api.Enums;
using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public class MessageFormatter : IMessageFormatter
{
    public string FormatAlert(BorderPoint borderPoint, TrafficSnapshot latest, TrendResult trend)
    {
        var baseMessage = $"\uD83D\uDE97 {borderPoint.Name} \u2192 {latest.EstimatedDelayMinutes} min.";
        var trendSentence = trend.PredictionLabel switch
        {
            "hausse probable" => "Hausse probable.",
            "am\u00e9lioration probable" => "Am\u00e9lioration probable.",
            _ => "Stable."
        };

        string extra;
        if (latest.EstimatedDelayMinutes >= 25)
        {
            extra = "\u00c9vite si tu peux.";
        }
        else if (latest.EstimatedDelayMinutes < 10 && trend.Trend == TrendDirection.Stable)
        {
            extra = "Fluide.";
        }
        else
        {
            extra = string.Empty;
        }

        var message = string.IsNullOrWhiteSpace(extra)
            ? $"{baseMessage} {trendSentence}"
            : $"{baseMessage} {trendSentence} {extra}";

        var hashtags = BuildHashtags(borderPoint.Name);

        return hashtags.Count == 0 ? message : $"{message} {string.Join(' ', hashtags)}";
    }

    private static List<string> BuildHashtags(string borderPointName)
    {
        var tags = new List<string>();

        var localTag = borderPointName switch
        {
            "Moillesulaz" or "Th\u00f4nex-Vallard" => "#Annemasse",
            "Meyrin" or "Ferney-Voltaire" => "#PaysDeGex",
            "Bardonnex" or "Perly" => "#Geneve",
            "Ani\u00e8res" => "#Chablais",
            _ => "#Geneve"
        };

        tags.Add(localTag);

        if (borderPointName is "Bardonnex" or "Perly" or "Moillesulaz" or "Th\u00f4nex-Vallard")
        {
            tags.Add("#Frontiere");
        }

        return tags.Take(2).ToList();
    }
}
