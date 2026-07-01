using FrontiereLiveGe.Api.Data;
using FrontiereLiveGe.Api.Endpoints;
using FrontiereLiveGe.Api.Services;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
builder.Services.Configure<XOptions>(builder.Configuration.GetSection("X"));
builder.Services.AddHttpClient("XOAuth", client =>
{
    client.BaseAddress = new Uri("https://api.x.com/2/oauth2/");
});
builder.Services.AddHttpClient("XApi", client =>
{
    client.BaseAddress = new Uri("https://api.x.com/2/");
});
builder.Services.AddHttpClient<IPostPublisher, XPostPublisher>(client =>
{
    client.BaseAddress = new Uri("https://api.x.com/2/");
});
builder.Services.AddSingleton<IXTokenService, XTokenService>();
builder.Services.AddScoped<IBorderRadarRunner, BorderRadarRunner>();

builder.Services.AddHostedService<BotWorker>();

var app = builder.Build();

app.UseCors("ViteCors");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapApiEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

await app.RunAsync();
