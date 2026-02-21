using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record PodcastStats
{
    [JsonPropertyName("podcast_name")]
    public required string PodcastName { get; init; }

    [JsonPropertyName("listen_count")]
    public required int ListenCount { get; init; }

    [JsonPropertyName("total_listen_time_ms")]
    public required long TotalListenTimeMs { get; init; }
}

public record WeeklyStats
{
    [JsonPropertyName("week_start")]
    public required string WeekStart { get; init; }

    [JsonPropertyName("listen_count")]
    public required int ListenCount { get; init; }

    [JsonPropertyName("total_listen_time_ms")]
    public required long TotalListenTimeMs { get; init; }
}

public record ListenSummary
{
    [JsonPropertyName("total_listens")]
    public required int TotalListens { get; init; }

    [JsonPropertyName("total_listen_time_ms")]
    public required long TotalListenTimeMs { get; init; }

    [JsonPropertyName("unique_podcasts")]
    public required int UniquePodcasts { get; init; }

    [JsonPropertyName("unique_episodes")]
    public required int UniqueEpisodes { get; init; }
}
