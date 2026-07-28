using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Security;

public sealed class AdminApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public AdminApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Admin API key is not configured."));
        }

        if (!Request.Headers.TryGetValue(AdminApiKeyDefaults.HeaderName, out var providedValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = providedValues.ToString();
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));

        if (!CryptographicOperations.FixedTimeEquals(configuredHash, providedHash))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "admin") },
            AdminApiKeyDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AdminApiKeyDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
