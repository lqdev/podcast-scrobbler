using PodcastScrobbler.Config;

namespace PodcastScrobbler.Middleware;

public class OptionalTokenAuth
{
    private readonly RequestDelegate _next;
    private readonly ScrobblerConfig _config;

    private static readonly HashSet<string> SkipAuthPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/ready",
        "/1/validate-token"
    };

    public OptionalTokenAuth(RequestDelegate next, ScrobblerConfig config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // If no token configured, skip auth entirely
        if (string.IsNullOrEmpty(_config.ScrobblerToken))
        {
            await _next(context);
            return;
        }

        // Skip auth for health/ready/validate-token
        var path = context.Request.Path.Value ?? "";
        if (SkipAuthPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // Skip auth for GET requests unless configured otherwise
        var isReadRequest = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (isReadRequest && !_config.RequireAuthForReads)
        {
            await _next(context);
            return;
        }

        // Extract token from Authorization header: "Token <value>"
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        string? token = null;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Token ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Token ".Length..].Trim();
        }

        if (string.Equals(token, _config.ScrobblerToken, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"code": 401, "error": "Unauthorized. Invalid or missing token."}""");
    }
}
