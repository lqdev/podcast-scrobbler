using PodcastScrobbler.Data;

namespace PodcastScrobbler.Endpoints;

public static class StatsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/1/user/{username}/stats/podcasts", async (string username, int? limit, ListenRepository repo) =>
        {
            var actualLimit = Math.Clamp(limit ?? 10, 1, 100);
            var stats = await repo.GetTopPodcasts(username, actualLimit);
            return Results.Ok(new { payload = stats });
        });

        app.MapGet("/1/user/{username}/stats/weekly", async (string username, int? weeks, ListenRepository repo) =>
        {
            var actualWeeks = Math.Clamp(weeks ?? 12, 1, 52);
            var stats = await repo.GetWeeklyStats(username, actualWeeks);
            return Results.Ok(new { payload = stats });
        });

        app.MapGet("/1/user/{username}/stats/recent-podcasts", async (string username, int? limit, ListenRepository repo) =>
        {
            var actualLimit = Math.Clamp(limit ?? 10, 1, 100);
            var podcasts = await repo.GetRecentPodcasts(username, actualLimit);
            return Results.Ok(new { payload = podcasts });
        });

        app.MapGet("/1/user/{username}/stats/summary", async (string username, ListenRepository repo) =>
        {
            var summary = await repo.GetSummary(username);
            return Results.Ok(new { payload = summary });
        });
    }
}
