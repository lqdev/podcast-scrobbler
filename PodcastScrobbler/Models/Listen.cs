namespace PodcastScrobbler.Models;

public record Listen
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Username { get; init; }
    public required long ListenedAt { get; init; }
    public required string ArtistName { get; init; }
    public required string TrackName { get; init; }
    public AdditionalInfo? AdditionalInfo { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
