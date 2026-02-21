namespace PodcastScrobbler.Models;

public record PlayingNow
{
    public required string Username { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string ArtistName { get; init; }
    public required string TrackName { get; init; }
    public AdditionalInfo? AdditionalInfo { get; init; }
}
