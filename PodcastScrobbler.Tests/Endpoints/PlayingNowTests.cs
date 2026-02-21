using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class PlayingNowTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PlayingNowTests(ScrobblerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Token test-token");
    }

    [Fact]
    public async Task GetPlayingNow_NoState_ReturnsEmpty()
    {
        var response = await _client.GetAsync("/1/user/nobody/playing-now");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, content.GetProperty("payload").GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task SetAndGetPlayingNow_RoundTrip()
    {
        var submitRequest = new
        {
            listen_type = "playing_now",
            payload = new[]
            {
                new
                {
                    track_metadata = new
                    {
                        artist_name = "Now Playing Podcast",
                        track_name = "Current Episode"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest);

        var response = await _client.GetAsync("/1/user/default/playing-now");
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, content.GetProperty("payload").GetProperty("count").GetInt32());

        var trackMetadata = content.GetProperty("payload").GetProperty("playing_now").GetProperty("track_metadata");
        Assert.Equal("Now Playing Podcast", trackMetadata.GetProperty("artist_name").GetString());
        Assert.Equal("Current Episode", trackMetadata.GetProperty("track_name").GetString());
    }
}
