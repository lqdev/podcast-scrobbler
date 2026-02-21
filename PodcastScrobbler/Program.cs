using Microsoft.AspNetCore.Diagnostics;
using PodcastScrobbler.Config;
using PodcastScrobbler.Data;
using PodcastScrobbler.Endpoints;
using PodcastScrobbler.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = new ScrobblerConfig();
builder.Configuration.Bind(config);
builder.Services.AddSingleton(config);

// Port configuration
var port = config.Port;
builder.WebHost.UseUrls($"http://+:{port}");

// DI registrations
builder.Services.AddSingleton<DuckDbContext>();
builder.Services.AddSingleton<PlayingNowStore>();
builder.Services.AddScoped<ListenRepository>();

// JSON serialization — snake_case for LB compat
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Initialize database
var db = app.Services.GetRequiredService<DuckDbContext>();
await db.Initialize();

// Load playing_now state
var playingNowStore = app.Services.GetRequiredService<PlayingNowStore>();
await playingNowStore.LoadFromDb();

// Startup diagnostics
app.Logger.LogInformation("Database: {Path}", config.DatabasePath);
app.Logger.LogInformation("Auth: {Status}", string.IsNullOrEmpty(config.ScrobblerToken) ? "disabled" : "enabled");
app.Logger.LogInformation("Listening on port {Port}", config.Port);

// Global exception handler — must be first in pipeline to catch all downstream exceptions
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        app.Logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"code": 500, "error": "Internal server error"}""");
    });
});

// Middleware
app.UseMiddleware<OptionalTokenAuth>();

// Map endpoints
SubmitListensEndpoint.Map(app);
GetListensEndpoint.Map(app);
PlayingNowEndpoint.Map(app);
ValidateTokenEndpoint.Map(app);
StatsEndpoints.Map(app);
HealthEndpoints.Map(app);

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program { }
