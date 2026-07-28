using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FrontiereLiveGe.Api.Models;

namespace FrontiereLiveGe.Api.Services;

public class XPostPublisher : IPostPublisher
{
    private readonly HttpClient _http;
    private readonly IXTokenService _tokenService;
    private readonly ILogger<XPostPublisher> _logger;

    public XPostPublisher(HttpClient http, IXTokenService tokenService, ILogger<XPostPublisher> logger)
    {
        _http = http;
        _tokenService = tokenService;
        _logger = logger;
    }

    public bool IsLive => true;

    public async Task PublishAsync(AlertEvent alert, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Skipping empty X message for alert {AlertId}.", alert.Id);
            return;
        }

        var token = await _tokenService.GetAccessTokenAsync(ct);
        var result = await TryPublishAsync(message, token, ct);

        if (!result.Success && result.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await _tokenService.TryRefreshAsync(ct, force: true);
            if (refreshed)
            {
                token = await _tokenService.GetAccessTokenAsync(ct);
                result = await TryPublishAsync(message, token, ct);
            }
        }

        if (!result.Success)
        {
            throw new InvalidOperationException($"X post failed with status {result.StatusCode}.");
        }
    }

    private async Task<XPostResult> TryPublishAsync(string message, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "tweets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { text = message }),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("X post published successfully.");
            return XPostResult.Ok();
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("X post unauthorized. Token may be expired.");
            return XPostResult.Failed(response.StatusCode);
        }

        _logger.LogWarning("X post failed with status {Status}.", response.StatusCode);
        return XPostResult.Failed(response.StatusCode);
    }

    private sealed record XPostResult(bool Success, HttpStatusCode? StatusCode)
    {
        public static XPostResult Ok() => new(true, null);

        public static XPostResult Failed(HttpStatusCode statusCode) => new(false, statusCode);
    }
}
