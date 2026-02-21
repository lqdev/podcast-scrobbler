using System.Text.Json.Serialization;

namespace PodcastScrobbler.Models;

public record ValidateTokenResponse
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("valid")]
    public required bool Valid { get; init; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }
}
