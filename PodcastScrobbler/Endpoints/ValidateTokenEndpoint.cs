using PodcastScrobbler.Config;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Endpoints;

public static class ValidateTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/1/validate-token", (HttpContext context, ScrobblerConfig config) =>
        {
            // Extract token from header or query param
            string? token = null;

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Token ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Token ".Length..].Trim();
            }

            token ??= context.Request.Query["token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return Results.BadRequest(new ValidateTokenResponse
                {
                    Code = 400,
                    Message = "No token provided.",
                    Valid = false
                });
            }

            if (string.IsNullOrEmpty(config.ScrobblerToken))
            {
                // No token configured — all tokens are valid
                return Results.Ok(new ValidateTokenResponse
                {
                    Code = 200,
                    Message = "Token valid.",
                    Valid = true,
                    UserName = "default"
                });
            }

            if (string.Equals(token, config.ScrobblerToken, StringComparison.Ordinal))
            {
                return Results.Ok(new ValidateTokenResponse
                {
                    Code = 200,
                    Message = "Token valid.",
                    Valid = true,
                    UserName = "default"
                });
            }

            return Results.Ok(new ValidateTokenResponse
            {
                Code = 200,
                Message = "Token invalid.",
                Valid = false
            });
        });
    }
}
