using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PodcastScrobbler.Config;

namespace PodcastScrobbler.Tests.Middleware;

public class OptionalTokenAuthTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly ScrobblerWebApplicationFactory _factory;

    public OptionalTokenAuthTests(ScrobblerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WriteEndpoint_WithValidToken_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Token test-token");

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
        var response = await client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WriteEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        // No auth header

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
        var response = await client.PostAsJsonAsync("/1/submit-listens", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoint_WithoutToken_Succeeds()
    {
        var client = _factory.CreateClient();
        // No auth header — reads should pass through

        var response = await client.GetAsync("/1/user/default/listens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_SkipsAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidateTokenEndpoint_SkipsAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/1/validate-token?token=test-token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoint_WithRequireAuthForReads_ReturnsUnauthorized()
    {
        await using var requireAuthFactory = new ScrobblerWebApplicationFactory()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ScrobblerConfig));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(new ScrobblerConfig
                {
                    DatabasePath = ":memory:",
                    ScrobblerToken = "test-token",
                    RequireAuthForReads = true,
                    Port = "5000"
                });
            }));

        var client = requireAuthFactory.CreateClient();
        // No Authorization header

        var response = await client.GetAsync("/1/user/default/listens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
