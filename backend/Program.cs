using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Endpoints;
using FrontiereLiveGe.Api.Security;
using FrontiereLiveGe.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.Configure<BotWorkerOptions>(builder.Configuration.GetSection("BotWorker"));

builder.Services.AddScoped<DbInitializer>();

builder.Services.AddScoped<ITrafficDataProvider, FakeTrafficDataProvider>();
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
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.Path.StartsWithSegments("/api/admin"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }
    await next();
});

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

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

await app.RunAsync();
