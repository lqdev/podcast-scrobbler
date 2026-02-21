using PodcastScrobbler.Data;

namespace PodcastScrobbler.Endpoints;

public static class GetListensEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/1/user/{username}/listens", async (string username, int? count, long? max_ts, long? min_ts, ListenRepository repo) =>
        {
            var actualCount = Math.Clamp(count ?? 25, 1, 100);

            var listens = await repo.GetListens(username, actualCount, max_ts, min_ts);

            var payload = listens.Select(l => new
            {
                listened_at = l.ListenedAt,
                track_metadata = new
                {
                    artist_name = l.ArtistName,
                    track_name = l.TrackName,
                    additional_info = l.AdditionalInfo
                }
            });

            return Results.Ok(new
            {
                payload = new
                {
                    count = listens.Count,
                    listens = payload
                }
            });
        });

        app.MapGet("/1/user/{username}/listen-count", async (string username, ListenRepository repo) =>
        {
            var count = await repo.GetListenCount(username);
            return Results.Ok(new
            {
                payload = new { count }
            });
        });
    }
}
