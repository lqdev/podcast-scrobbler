using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record TrackMetadata
{
    [JsonPropertyName("artist_name")]
    public required string ArtistName { get; init; }

    [JsonPropertyName("track_name")]
    public required string TrackName { get; init; }

    [JsonPropertyName("additional_info")]
    public AdditionalInfo? AdditionalInfo { get; init; }
}
