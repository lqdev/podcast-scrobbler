using PodcastScrobbler.Config;
using PodcastScrobbler.Data;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Tests.Data;

public class ListenRepositoryTests : IDisposable
{
    private readonly DuckDbContext _db;
    private readonly ListenRepository _repo;

    public ListenRepositoryTests()
    {
        var config = new ScrobblerConfig { DatabasePath = ":memory:" };
        _db = new DuckDbContext(config);
        _db.Initialize().GetAwaiter().GetResult();
        _repo = new ListenRepository(_db);
    }

    [Fact]
    public async Task InsertAndGetListen_Works()
    {
        var listen = new Listen
        {
            Username = "testuser",
            ListenedAt = 1740098330,
            ArtistName = "Test Podcast",
            TrackName = "Episode 1"
        };
        await _repo.InsertListen(listen);

        var listens = await _repo.GetListens("testuser", 10, null, null);
        Assert.Single(listens);
        Assert.Equal("Test Podcast", listens[0].ArtistName);
        Assert.Equal("Episode 1", listens[0].TrackName);
    }

    [Fact]
    public async Task GetListenCount_ReturnsCorrectCount()
    {
        await _repo.InsertListen(new Listen { Username = "counter", ListenedAt = 1, ArtistName = "A", TrackName = "1" });
        await _repo.InsertListen(new Listen { Username = "counter", ListenedAt = 2, ArtistName = "A", TrackName = "2" });

        var count = await _repo.GetListenCount("counter");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetTopPodcasts_GroupsByArtist()
    {
        await _repo.InsertListen(new Listen { Username = "stats", ListenedAt = 1, ArtistName = "Pod A", TrackName = "E1" });
        await _repo.InsertListen(new Listen { Username = "stats", ListenedAt = 2, ArtistName = "Pod A", TrackName = "E2" });
        await _repo.InsertListen(new Listen { Username = "stats", ListenedAt = 3, ArtistName = "Pod B", TrackName = "E1" });

        var top = await _repo.GetTopPodcasts("stats", 10);
        Assert.Equal(2, top.Count);
        Assert.Equal("Pod A", top[0].PodcastName);
        Assert.Equal(2, top[0].ListenCount);
    }

    [Fact]
    public async Task GetSummary_ReturnsAggregate()
    {
        await _repo.InsertListen(new Listen { Username = "sum", ListenedAt = 1, ArtistName = "P1", TrackName = "E1" });
        await _repo.InsertListen(new Listen { Username = "sum", ListenedAt = 2, ArtistName = "P1", TrackName = "E2" });
        await _repo.InsertListen(new Listen { Username = "sum", ListenedAt = 3, ArtistName = "P2", TrackName = "E3" });

        var summary = await _repo.GetSummary("sum");
        Assert.Equal(3, summary.TotalListens);
        Assert.Equal(2, summary.UniquePodcasts);
        Assert.Equal(3, summary.UniqueEpisodes);
    }

    [Fact]
    public async Task InsertListenWithAdditionalInfo_RoundTrips()
    {
        var listen = new Listen
        {
            Username = "info",
            ListenedAt = 1,
            ArtistName = "Pod",
            TrackName = "Ep",
            AdditionalInfo = new AdditionalInfo
            {
                MediaPlayer = "podcast-tui",
                DurationMs = 3600000,
                PositionMs = 1800000
            }
        };
        await _repo.InsertListen(listen);

        var listens = await _repo.GetListens("info", 1, null, null);
        Assert.NotNull(listens[0].AdditionalInfo);
        Assert.Equal("podcast-tui", listens[0].AdditionalInfo!.MediaPlayer);
        Assert.Equal(3600000, listens[0].AdditionalInfo!.DurationMs);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
