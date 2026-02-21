using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class ValidateTokenTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ValidateTokenTests(ScrobblerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsValid()
    {
        _client.DefaultRequestHeaders.Add("Authorization", "Token test-token");
        var response = await _client.GetAsync("/1/validate-token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("valid").GetBoolean());
        Assert.Equal("default", content.GetProperty("user_name").GetString());
    }

    [Fact]
    public async Task ValidateToken_InvalidToken_ReturnsInvalid()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/1/validate-token");
        request.Headers.Add("Authorization", "Token wrong-token");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(content.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task ValidateToken_NoToken_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/1/validate-token");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidateToken_QueryParam_ReturnsValid()
    {
        var response = await _client.GetAsync("/1/validate-token?token=test-token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("valid").GetBoolean());
    }
}
