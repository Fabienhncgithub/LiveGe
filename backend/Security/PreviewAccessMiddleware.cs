using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FrontiereLiveGe.Api.Security;

public sealed class PreviewAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PreviewAccessOptions _options;

    public PreviewAccessMiddleware(RequestDelegate next, IOptions<PreviewAccessOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled
            || context.Request.Path.StartsWithSegments("/health")
            || HasValidCredentials(context.Request.Headers.Authorization))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = """Basic realm="Frontiere Live GE", charset="UTF-8" """;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsync("Authentification requise.");
    }

    private bool HasValidCredentials(string? authorization)
    {
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separator = decoded.IndexOf(':');
            if (separator < 0)
            {
                return false;
            }

            var username = decoded[..separator];
            var password = decoded[(separator + 1)..];
            return FixedTimeEquals(username, _options.Username)
                && FixedTimeEquals(password, _options.Password);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
