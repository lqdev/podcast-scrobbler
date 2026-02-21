using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record AdditionalInfo
{
    [JsonPropertyName("media_player")]
    public string? MediaPlayer { get; init; }

    [JsonPropertyName("podcast_feed_url")]
    public string? PodcastFeedUrl { get; init; }

    [JsonPropertyName("episode_guid")]
    public string? EpisodeGuid { get; init; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("position_ms")]
    public long? PositionMs { get; init; }

    [JsonPropertyName("percent_complete")]
    public double? PercentComplete { get; init; }
}
