using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Endpoints;
using FrontiereLiveGe.Api.Security;
using FrontiereLiveGe.Api.Services;
using FrontiereLiveGe.Api.Services.PublicData;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Query-string API keys must never appear in routine HttpClient request logs.
builder.Logging.AddFilter("System.Net.Http.HttpClient.HereTraffic", LogLevel.Warning);

if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var platformPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddAuthentication(AdminApiKeyDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyDefaults.AuthenticationScheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminApiKeyDefaults.AuthorizationPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(AdminApiKeyDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("public", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("admin", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteCors", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:5173" };

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddOptions<BotWorkerOptions>()
    .Bind(builder.Configuration.GetSection("BotWorker"))
    .Validate(options => options.IntervalMinutes is >= 30 and <= 1440,
        "BotWorker:IntervalMinutes must be between 30 and 1440.")
    .ValidateOnStart();
builder.Services
    .AddOptions<PreviewAccessOptions>()
    .Bind(builder.Configuration.GetSection(PreviewAccessOptions.SectionName))
    .Validate(options => !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.Username) && options.Password.Length >= 16),
        "Preview access requires a username and a password of at least 16 characters.")
    .ValidateOnStart();
builder.Services
    .AddOptions<HereTrafficOptions>()
    .Bind(builder.Configuration.GetSection(HereTrafficOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
        "Traffic:Here:ApiKey is required when HERE traffic is enabled.")
    .Validate(options => options.CacheSeconds >= 1800,
        "Traffic:Here:CacheSeconds must be at least 1800 to protect the free quota.")
    .Validate(options => options.MaxRequestsPerDay is >= 14 and <= 600,
        "Traffic:Here:MaxRequestsPerDay must be between 14 and 600.")
    .Validate(options => !options.Enabled
        || IsAllowedHttpsEndpoint(options.BaseUrl, "router.hereapi.com"),
        "Traffic:Here:BaseUrl must use https://router.hereapi.com.")
    .ValidateOnStart();

builder.Services.AddScoped<DbInitializer>();

builder.Services.AddScoped<ITrafficDataProvider, HereTrafficDataProvider>();
builder.Services.AddHttpClient("HereTraffic", (services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HereTrafficOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddSingleton<IDirectionalTrafficService, HereDirectionalTrafficService>();
builder.Services.AddHttpClient("GenevaRoadworks", client =>
{
    client.BaseAddress = new Uri(
        "https://app2.ge.ch/tergeoservices/rest/services/Hosted/INFOMOB_CHANTIER_POINT/FeatureServer/0/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FrontiereLiveGE/1.0");
});
builder.Services.AddHttpClient("BisonFute", client =>
{
    client.BaseAddress = new Uri(
        "https://tipi.bison-fute.gouv.fr/bison-fute-ouvert/publicationsDIR/Evenementiel-DIR/grt/RRN/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FrontiereLiveGE/1.0");
});
builder.Services.AddHttpClient("MeteoSwiss", client =>
{
    client.BaseAddress = new Uri("https://data.geo.admin.ch/ch.meteoschweiz.ogd-smn/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FrontiereLiveGE/1.0");
});
builder.Services.AddSingleton<GenevaRoadworksProvider>();
builder.Services.AddSingleton<BisonFuteProvider>();
builder.Services.AddSingleton<MeteoSwissProvider>();
builder.Services.AddSingleton<IPublicDataProvider>(services =>
    services.GetRequiredService<GenevaRoadworksProvider>());
builder.Services.AddSingleton<IPublicDataProvider>(services =>
    services.GetRequiredService<BisonFuteProvider>());
builder.Services.AddSingleton<IPublicDataProvider>(services =>
    services.GetRequiredService<MeteoSwissProvider>());
builder.Services.AddSingleton<IRoadContextService, RoadContextService>();
builder.Services.AddSingleton<IMobilityAdviceService, MobilityAdviceService>();
builder.Services.AddScoped<ITrafficIngestionService, TrafficIngestionService>();
builder.Services.AddScoped<ITrendAnalyzer, TrendAnalyzer>();
builder.Services.AddScoped<IAlertEngine, AlertEngine>();
builder.Services.AddScoped<IMessageFormatter, MessageFormatter>();
builder.Services
    .AddOptions<XOptions>()
    .Bind(builder.Configuration.GetSection("X"))
    .Validate(
        options => !options.Enabled
            || (!string.IsNullOrWhiteSpace(options.ClientId)
                && !string.IsNullOrWhiteSpace(options.ClientSecret)
                && !string.IsNullOrWhiteSpace(options.AccessToken)
                && !string.IsNullOrWhiteSpace(options.RefreshToken)),
        "X OAuth requires ClientId, ClientSecret, AccessToken and RefreshToken when enabled.")
    .Validate(
        options => !options.Enabled
            || (IsAllowedHttpsEndpoint(options.ApiBaseUrl, "api.x.com")
                && IsAllowedHttpsEndpoint(options.OAuthBaseUrl, "api.x.com")),
        "X API endpoints must use https://api.x.com.")
    .ValidateOnStart();
builder.Services.AddHttpClient("XOAuth", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["X:OAuthBaseUrl"] ?? "https://api.x.com/2/oauth2/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("XApi", client =>
{
    client.BaseAddress = new Uri($"{(builder.Configuration["X:ApiBaseUrl"] ?? "https://api.x.com/2").TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

if (builder.Configuration.GetValue<bool>("X:Enabled"))
{
    builder.Services.AddHttpClient<IPostPublisher, XPostPublisher>(client =>
    {
        client.BaseAddress = new Uri($"{(builder.Configuration["X:ApiBaseUrl"] ?? "https://api.x.com/2").TrimEnd('/')}/");
        client.Timeout = TimeSpan.FromSeconds(15);
    });
}
else
{
    builder.Services.AddScoped<IPostPublisher, FakePostPublisher>();
}

builder.Services.AddSingleton<IXTokenService, XTokenService>();
builder.Services.AddSingleton<RadarRunGate>();
builder.Services.AddScoped<IBorderRadarRunner, BorderRadarRunner>();

builder.Services.AddHostedService<BotWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Security:UseHttpsRedirection"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
        "script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob: https://tiles.openfreemap.org; " +
        "connect-src 'self' https://tiles.openfreemap.org; worker-src 'self' blob:; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }
    await next();
});

app.UseMiddleware<PreviewAccessMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("ViteCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", database = "connected" })
        : Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503))
    .RequireRateLimiting("public");

app.MapApiEndpoints();
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

await app.RunAsync();

static bool IsAllowedHttpsEndpoint(string value, string expectedHost) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && uri.Scheme == Uri.UriSchemeHttps
    && uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase);
