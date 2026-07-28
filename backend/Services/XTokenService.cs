using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Services;

public class XTokenService : IXTokenService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<XTokenService> _logger;
    private readonly XOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string _accessToken;
    private string _refreshToken;
    private DateTime? _expiresAtUtc;

    public XTokenService(IHttpClientFactory httpClientFactory, IOptions<XOptions> options, ILogger<XTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;

        _accessToken = _options.AccessToken;
        _refreshToken = _options.RefreshToken;
        _expiresAtUtc = _options.AccessTokenExpiresAtUtc;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new InvalidOperationException("X OAuth2 access token is missing. Set X:AccessToken in user secrets or config.");
        }

        if (IsExpiringSoon() && !string.IsNullOrWhiteSpace(_refreshToken))
        {
            await TryRefreshAsync(ct);
        }

        return _accessToken;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            _logger.LogWarning("X refresh token not set. Skipping refresh.");
            return false;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (!force && !IsExpiringSoon())
            {
                return true;
            }

            var client = _httpClientFactory.CreateClient("XOAuth");
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _options.ClientId,
                ["refresh_token"] = _refreshToken
            };

            using var content = new FormUrlEncodedContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            using var request = new HttpRequestMessage(HttpMethod.Post, "token")
            {
                Content = content
            };

            if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("X token refresh failed with status {Status}.", response.StatusCode);
                return false;
            }

            var token = JsonSerializer.Deserialize<XTokenResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                _logger.LogWarning("X token refresh response missing access token.");
                return false;
            }

            _accessToken = token.AccessToken;
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                _refreshToken = token.RefreshToken;
            }

            if (token.ExpiresIn > 0)
            {
                _expiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            }

            _logger.LogInformation("X OAuth2 token refreshed. Update user secrets with new tokens if needed.");
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsExpiringSoon()
    {
        if (!_expiresAtUtc.HasValue)
        {
            return false;
        }

        return DateTime.UtcNow >= _expiresAtUtc.Value.AddMinutes(-2);
    }

    private sealed class XTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
