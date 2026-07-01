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

    public async Task PublishAsync(AlertEvent alert, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Skipping empty X message for alert {AlertId}.", alert.Id);
            return;
        }

        var token = await _tokenService.GetAccessTokenAsync(ct);
        var result = await TryPublishAsync(message, token, ct);

        if (!result.Success)
        {
            var refreshed = await _tokenService.TryRefreshAsync(ct);
            if (refreshed)
            {
                token = await _tokenService.GetAccessTokenAsync(ct);
                result = await TryPublishAsync(message, token, ct);
            }
        }

        if (!result.Success)
        {
            throw new InvalidOperationException($"X post failed after refresh attempt. Status: {result.StatusCode}. Response: {result.Body}");
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

        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("X post unauthorized. Token may be expired. Response: {Body}", body);
            return XPostResult.Failed(response.StatusCode, body);
        }

        _logger.LogWarning("X post failed: {Status} {Body}", response.StatusCode, body);
        return XPostResult.Failed(response.StatusCode, body);
    }

    private sealed record XPostResult(bool Success, HttpStatusCode? StatusCode, string Body)
    {
        public static XPostResult Ok() => new(true, null, string.Empty);

        public static XPostResult Failed(HttpStatusCode statusCode, string body) => new(false, statusCode, body);
    }
}
