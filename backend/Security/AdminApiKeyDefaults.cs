namespace FrontiereLiveGe.Api.Security;

public static class AdminApiKeyDefaults
{
    public const string AuthenticationScheme = "AdminApiKey";
    public const string AuthorizationPolicy = "Admin";
    public const string HeaderName = "X-Admin-Key";
}
