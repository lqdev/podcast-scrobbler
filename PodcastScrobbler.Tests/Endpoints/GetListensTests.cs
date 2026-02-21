using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class GetListensTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetListensTests(ScrobblerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Token test-token");
    }

    [Fact]
    public async Task GetListens_EmptyDb_ReturnsEmptyPayload()
    {
        var response = await _client.GetAsync("/1/user/nonexistent-user/listens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payload = content.GetProperty("payload");
        Assert.Equal(0, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetListenCount_ReturnsCount()
    {
        var response = await _client.GetAsync("/1/user/default/listen-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var count = content.GetProperty("payload").GetProperty("count").GetInt32();
        Assert.True(count >= 0);
    }

    [Fact]
    public async Task SubmitAndGetListens_RoundTrip()
    {
        // Submit
        var submitRequest = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1740099999L,
                    track_metadata = new
                    {
                        artist_name = "RoundTrip Podcast",
                        track_name = "RoundTrip Episode"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest);

        // Get
        var response = await _client.GetAsync("/1/user/default/listens");
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var listens = content.GetProperty("payload").GetProperty("listens");
        Assert.True(listens.GetArrayLength() > 0);
    }
}
