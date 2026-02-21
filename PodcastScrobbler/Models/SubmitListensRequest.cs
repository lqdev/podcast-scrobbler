using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record SubmitListensRequest
{
    [JsonPropertyName("listen_type")]
    public required string ListenType { get; init; }

    [JsonPropertyName("payload")]
    public required List<ListenPayload> Payload { get; init; }
}

public record ListenPayload
{
    [JsonPropertyName("listened_at")]
    public long? ListenedAt { get; init; }

    [JsonPropertyName("track_metadata")]
    public required TrackMetadata TrackMetadata { get; init; }
}
