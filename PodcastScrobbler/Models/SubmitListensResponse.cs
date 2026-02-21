using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record SubmitListensResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
