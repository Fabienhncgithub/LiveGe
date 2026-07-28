namespace FrontiereLiveGe.Api.Services;

public class XOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.x.com/2";
    public string OAuthBaseUrl { get; set; } = "https://api.x.com/2/oauth2";
}
