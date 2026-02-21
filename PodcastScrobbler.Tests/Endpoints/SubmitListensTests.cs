using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class SubmitListensTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SubmitListensTests(ScrobblerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Token test-token");
    }

    [Fact]
    public async Task SubmitSingleListen_ReturnsOk()
    {
        var request = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1740098330L,
                    track_metadata = new
                    {
                        artist_name = "Test Podcast",
                        track_name = "Episode 1"
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SubmitPlayingNow_ReturnsOk()
    {
        var request = new
        {
            listen_type = "playing_now",
            payload = new[]
            {
                new
                {
                    track_metadata = new
                    {
                        artist_name = "Test Podcast",
                        track_name = "Episode 2"
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SubmitImport_ReturnsOk()
    {
        var request = new
        {
            listen_type = "import",
            payload = new[]
            {
                new
                {
                    listened_at = 1740098330L,
                    track_metadata = new { artist_name = "Podcast A", track_name = "Ep 1" }
                },
                new
                {
                    listened_at = 1740098430L,
                    track_metadata = new { artist_name = "Podcast A", track_name = "Ep 2" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SubmitInvalidListenType_ReturnsBadRequest()
    {
        var request = new
        {
            listen_type = "invalid",
            payload = new[]
            {
                new
                {
                    track_metadata = new { artist_name = "Test", track_name = "Test" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitWithoutAuth_ReturnsUnauthorized()
    {
        var client = _client; // reuse but without auth
        var noAuthClient = new HttpClient(new HttpClientHandler()) { BaseAddress = _client.BaseAddress };
        // Create a new client without token via the factory
        var request = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1740098330L,
                    track_metadata = new { artist_name = "Test", track_name = "Test" }
                }
            }
        };

        // Use the existing client but remove auth header
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/1/submit-listens");
        httpRequest.Content = JsonContent.Create(request);
        // No Authorization header

        var factory = new ScrobblerWebApplicationFactory();
        var noAuthHttpClient = factory.CreateClient();
        var response = await noAuthHttpClient.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
