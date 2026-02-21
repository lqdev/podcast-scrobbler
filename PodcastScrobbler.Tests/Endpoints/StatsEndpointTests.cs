using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodcastScrobbler.Tests.Endpoints;

public class StatsEndpointTests : IClassFixture<ScrobblerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StatsEndpointTests(ScrobblerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Token test-token");
    }

    private async Task SubmitListen(string podcastName, string episodeName, long listenedAt, long? durationMs = null)
    {
        var payload = durationMs.HasValue
            ? new object[]
            {
                new
                {
                    listened_at = listenedAt,
                    track_metadata = new
                    {
                        artist_name = podcastName,
                        track_name = episodeName,
                        additional_info = new { duration_ms = durationMs.Value }
                    }
                }
            }
            : new object[]
            {
                new
                {
                    listened_at = listenedAt,
                    track_metadata = new { artist_name = podcastName, track_name = episodeName }
                }
            };

        await _client.PostAsJsonAsync("/1/submit-listens", new { listen_type = "single", payload });
    }

    [Fact]
    public async Task Summary_ReturnsSnakeCasePropertyNames()
    {
        var response = await _client.GetAsync("/1/user/default/stats/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payload = body.GetProperty("payload");

        // Verify snake_case keys exist
        Assert.True(payload.TryGetProperty("total_listens", out _), "Expected 'total_listens' (snake_case)");
        Assert.True(payload.TryGetProperty("total_listen_time_ms", out _), "Expected 'total_listen_time_ms' (snake_case)");
        Assert.True(payload.TryGetProperty("unique_podcasts", out _), "Expected 'unique_podcasts' (snake_case)");
        Assert.True(payload.TryGetProperty("unique_episodes", out _), "Expected 'unique_episodes' (snake_case)");

        // Verify PascalCase keys do NOT exist
        Assert.False(payload.TryGetProperty("TotalListens", out _), "Found PascalCase 'TotalListens' — should be snake_case");
        Assert.False(payload.TryGetProperty("TotalListenTimeMs", out _), "Found PascalCase 'TotalListenTimeMs' — should be snake_case");
        Assert.False(payload.TryGetProperty("UniquePodcasts", out _), "Found PascalCase 'UniquePodcasts' — should be snake_case");
        Assert.False(payload.TryGetProperty("UniqueEpisodes", out _), "Found PascalCase 'UniqueEpisodes' — should be snake_case");
    }

    [Fact]
    public async Task Summary_WithListens_ReturnsCorrectCounts()
    {
        await SubmitListen("Stats Pod A", "Ep 1", 1740100001L, 3600000L);
        await SubmitListen("Stats Pod A", "Ep 2", 1740100002L, 1800000L);
        await SubmitListen("Stats Pod B", "Ep 1", 1740100003L);

        var response = await _client.GetAsync("/1/user/default/stats/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payload = body.GetProperty("payload");

        Assert.True(payload.GetProperty("total_listens").GetInt32() >= 3);
        Assert.True(payload.GetProperty("unique_podcasts").GetInt32() >= 2);
        Assert.True(payload.GetProperty("unique_episodes").GetInt32() >= 2); // "Ep 1" + "Ep 2" = 2 distinct track_names
    }

    [Fact]
    public async Task TopPodcasts_ReturnsSnakeCasePropertyNames()
    {
        await SubmitListen("SnakeCase Pod", "Ep 1", 1740200001L);

        var response = await _client.GetAsync("/1/user/default/stats/podcasts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("payload").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var first = items[0];
        Assert.True(first.TryGetProperty("podcast_name", out _), "Expected 'podcast_name' (snake_case)");
        Assert.True(first.TryGetProperty("listen_count", out _), "Expected 'listen_count' (snake_case)");
        Assert.True(first.TryGetProperty("total_listen_time_ms", out _), "Expected 'total_listen_time_ms' (snake_case)");

        Assert.False(first.TryGetProperty("PodcastName", out _), "Found PascalCase 'PodcastName'");
        Assert.False(first.TryGetProperty("ListenCount", out _), "Found PascalCase 'ListenCount'");
        Assert.False(first.TryGetProperty("TotalListenTimeMs", out _), "Found PascalCase 'TotalListenTimeMs'");
    }

    [Fact]
    public async Task WeeklyStats_ReturnsSnakeCasePropertyNames()
    {
        // Use a recent timestamp so it falls within the default 12-week window
        var recentTs = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        await SubmitListen("Weekly Pod", "Ep 1", recentTs);

        var response = await _client.GetAsync("/1/user/default/stats/weekly");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("payload").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var first = items[0];
        Assert.True(first.TryGetProperty("week_start", out _), "Expected 'week_start' (snake_case)");
        Assert.True(first.TryGetProperty("listen_count", out _), "Expected 'listen_count' (snake_case)");
        Assert.True(first.TryGetProperty("total_listen_time_ms", out _), "Expected 'total_listen_time_ms' (snake_case)");

        Assert.False(first.TryGetProperty("WeekStart", out _), "Found PascalCase 'WeekStart'");
        Assert.False(first.TryGetProperty("ListenCount", out _), "Found PascalCase 'ListenCount'");
        Assert.False(first.TryGetProperty("TotalListenTimeMs", out _), "Found PascalCase 'TotalListenTimeMs'");
    }
}
