using PodcastScrobbler.Data;

namespace PodcastScrobbler.Endpoints;

public static class PlayingNowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/1/user/{username}/playing-now", (string username, PlayingNowStore store) =>
        {
            var playingNow = store.Get(username);

            if (playingNow is null)
            {
                return Results.Ok(new
                {
                    payload = new
                    {
                        count = 0,
                        playing_now = (object?)null
                    }
                });
            }

            return Results.Ok(new
            {
                payload = new
                {
                    count = 1,
                    playing_now = new
                    {
                        track_metadata = new
                        {
                            artist_name = playingNow.ArtistName,
                            track_name = playingNow.TrackName,
                            additional_info = playingNow.AdditionalInfo
                        }
                    }
                }
            });
        });
    }
}
