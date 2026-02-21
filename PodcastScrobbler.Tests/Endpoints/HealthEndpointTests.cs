using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class HealthEndpointTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly ScrobblerWebApplicationFactory _factory;

    public HealthEndpointTests(ScrobblerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Returns200WithHealthyStatus()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Token test-token");

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_Returns200WithReadyStatus()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Token test-token");

        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ready", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_DoesNotRequireAuth()
    {
        var client = _factory.CreateClient();
        // No Authorization header

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_DoesNotRequireAuth()
    {
        var client = _factory.CreateClient();
        // No Authorization header

        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
