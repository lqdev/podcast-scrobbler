using PodcastScrobbler.Config;
using PodcastScrobbler.Data;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Tests.Data;

public class PlayingNowStoreTests : IDisposable
{
    private readonly DuckDbContext _db;
    private readonly PlayingNowStore _store;

    public PlayingNowStoreTests()
    {
        var config = new ScrobblerConfig { DatabasePath = ":memory:" };
        _db = new DuckDbContext(config);
        _db.Initialize().GetAwaiter().GetResult();
        _store = new PlayingNowStore(_db);
    }

    [Fact]
    public void Get_NoState_ReturnsNull()
    {
        var result = _store.Get("nobody");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGet_Works()
    {
        var playingNow = new PlayingNow
        {
            Username = "testuser",
            ArtistName = "Test Pod",
            TrackName = "Current Ep"
        };
        await _store.Set(playingNow);

        var result = _store.Get("testuser");
        Assert.NotNull(result);
        Assert.Equal("Test Pod", result!.ArtistName);
        Assert.Equal("Current Ep", result.TrackName);
    }

    [Fact]
    public async Task SetOverwrites_PreviousState()
    {
        await _store.Set(new PlayingNow { Username = "user", ArtistName = "Pod1", TrackName = "Ep1" });
        await _store.Set(new PlayingNow { Username = "user", ArtistName = "Pod2", TrackName = "Ep2" });

        var result = _store.Get("user");
        Assert.Equal("Pod2", result!.ArtistName);
    }

    [Fact]
    public async Task LoadFromDb_RestoresState()
    {
        await _store.Set(new PlayingNow { Username = "persist", ArtistName = "Pod", TrackName = "Ep" });

        // Create a new store from the same DB to simulate restart
        var newStore = new PlayingNowStore(_db);
        await newStore.LoadFromDb();

        var result = newStore.Get("persist");
        Assert.NotNull(result);
        Assert.Equal("Pod", result!.ArtistName);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
