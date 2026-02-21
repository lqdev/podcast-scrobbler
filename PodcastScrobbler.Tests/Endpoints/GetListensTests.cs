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

    [Fact]
    public async Task GetListens_BothMaxTsAndMinTs_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/1/user/default/listens?max_ts=1740099999&min_ts=1740098000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("max_ts", body.GetProperty("error").GetString());
        Assert.Contains("min_ts", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetListens_CountClampedToMinimum()
    {
        // Seed 2 listens so the clamp is observable: count=0 → clamped to 1 → only 1 returned
        await _client.PostAsJsonAsync("/1/submit-listens", new
        {
            listen_type = "import",
            payload = new[]
            {
                new { listened_at = 1740600001L, track_metadata = new { artist_name = "Clamp Min Pod", track_name = "Ep 1" } },
                new { listened_at = 1740600002L, track_metadata = new { artist_name = "Clamp Min Pod", track_name = "Ep 2" } }
            }
        });

        var response = await _client.GetAsync("/1/user/default/listens?count=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payload = content.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetListens_CountClampedToMaximum()
    {
        // Seed 101 listens so the clamp is observable: count=200 → clamped to 100 → exactly 100 returned
        var payloadItems = Enumerable.Range(0, 101)
            .Select(i => (object)new { listened_at = 1740700000L + i, track_metadata = new { artist_name = "Clamp Max Pod", track_name = $"Ep {i}" } })
            .ToArray();
        await _client.PostAsJsonAsync("/1/submit-listens", new { listen_type = "import", payload = payloadItems });

        var response = await _client.GetAsync("/1/user/default/listens?count=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payload = content.GetProperty("payload");
        Assert.Equal(100, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetListens_MaxTsFiltersResults()
    {
        // Submit two listens
        var submitRequest1 = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1740400001L,
                    track_metadata = new
                    {
                        artist_name = "MaxTs Test Pod",
                        track_name = "Episode 1"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest1);

        var submitRequest2 = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1740400003L,
                    track_metadata = new
                    {
                        artist_name = "MaxTs Test Pod",
                        track_name = "Episode 2"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest2);

        // Query with max_ts=1740400002
        var response = await _client.GetAsync("/1/user/default/listens?max_ts=1740400002");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var listens = content.GetProperty("payload").GetProperty("listens").EnumerateArray().ToList();

        var listen1740400001 = listens.Any(l => l.GetProperty("listened_at").GetInt64() == 1740400001L);
        var listen1740400003 = listens.Any(l => l.GetProperty("listened_at").GetInt64() == 1740400003L);

        Assert.True(listen1740400001, "Listen at 1740400001 should appear");
        Assert.False(listen1740400003, "Listen at 1740400003 should not appear (max_ts filters to <)");
    }

    [Fact]
    public async Task GetListens_MinTsFiltersResults()
    {
        // Submit two listens
        // Use timestamps well above all other tests' ranges to avoid pagination interference
        var submitRequest1 = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1741100001L,
                    track_metadata = new
                    {
                        artist_name = "MinTs Test Pod",
                        track_name = "Episode 1"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest1);

        var submitRequest2 = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1741100003L,
                    track_metadata = new
                    {
                        artist_name = "MinTs Test Pod",
                        track_name = "Episode 2"
                    }
                }
            }
        };
        await _client.PostAsJsonAsync("/1/submit-listens", submitRequest2);

        // Query with min_ts=1741100002
        var response = await _client.GetAsync("/1/user/default/listens?min_ts=1741100002");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var listens = content.GetProperty("payload").GetProperty("listens").EnumerateArray().ToList();

        var listen1741100001 = listens.Any(l => l.GetProperty("listened_at").GetInt64() == 1741100001L);
        var listen1741100003 = listens.Any(l => l.GetProperty("listened_at").GetInt64() == 1741100003L);

        Assert.False(listen1741100001, "Listen at 1741100001 should not appear (min_ts filters to >)");
        Assert.True(listen1741100003, "Listen at 1741100003 should appear");
    }
}
