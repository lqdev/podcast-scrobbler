using PodcastScrobbler.Data;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Endpoints;

public static class SubmitListensEndpoint
{
    private static readonly HashSet<string> ValidListenTypes = new() { "playing_now", "single", "import" };

    public static void Map(WebApplication app)
    {
        app.MapPost("/1/submit-listens", async (SubmitListensRequest request, ListenRepository repo, PlayingNowStore playingNowStore) =>
        {
            if (!ValidListenTypes.Contains(request.ListenType))
            {
                return Results.BadRequest(new { error = $"Invalid listen_type: '{request.ListenType}'. Must be 'playing_now', 'single', or 'import'." });
            }

            if (request.Payload is null || request.Payload.Count == 0)
            {
                return Results.BadRequest(new { error = "Payload must contain at least one item." });
            }

            switch (request.ListenType)
            {
                case "playing_now":
                {
                    var item = request.Payload[0];
                    var playingNow = new PlayingNow
                    {
                        Username = "default",
                        ArtistName = item.TrackMetadata.ArtistName,
                        TrackName = item.TrackMetadata.TrackName,
                        AdditionalInfo = item.TrackMetadata.AdditionalInfo
                    };
                    await playingNowStore.Set(playingNow);
                    break;
                }

                case "single":
                {
                    var item = request.Payload[0];
                    if (!item.ListenedAt.HasValue)
                    {
                        return Results.BadRequest(new { error = "listened_at is required for listen_type 'single'." });
                    }

                    var listen = new Listen
                    {
                        Username = "default",
                        ListenedAt = item.ListenedAt.Value,
                        ArtistName = item.TrackMetadata.ArtistName,
                        TrackName = item.TrackMetadata.TrackName,
                        AdditionalInfo = item.TrackMetadata.AdditionalInfo
                    };
                    await repo.InsertListen(listen);
                    break;
                }

                case "import":
                {
                    var listens = request.Payload
                        .Where(p => p.ListenedAt.HasValue)
                        .Select(p => new Listen
                        {
                            Username = "default",
                            ListenedAt = p.ListenedAt!.Value,
                            ArtistName = p.TrackMetadata.ArtistName,
                            TrackName = p.TrackMetadata.TrackName,
                            AdditionalInfo = p.TrackMetadata.AdditionalInfo
                        });
                    await repo.InsertListens(listens);
                    break;
                }
            }

            return Results.Ok(new SubmitListensResponse { Status = "ok" });
        });
    }
}
