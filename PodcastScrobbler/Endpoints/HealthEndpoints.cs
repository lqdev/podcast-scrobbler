using PodcastScrobbler.Data;

namespace PodcastScrobbler.Endpoints;

public static class HealthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.MapGet("/ready", (DuckDbContext db) =>
        {
            try
            {
                var conn = db.GetReadConnection();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    cmd.ExecuteScalar();
                    return Results.Ok(new { status = "ready" });
                }
                finally
                {
                    if (!db.IsInMemory) conn.Dispose();
                }
            }
            catch
            {
                return Results.StatusCode(503);
            }
        });
    }
}
