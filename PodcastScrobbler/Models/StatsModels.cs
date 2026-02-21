namespace PodcastScrobbler.Models;

public record PodcastStats
{
    public required string PodcastName { get; init; }
    public required int ListenCount { get; init; }
    public required long TotalListenTimeMs { get; init; }
}

public record WeeklyStats
{
    public required string WeekStart { get; init; }
    public required int ListenCount { get; init; }
    public required long TotalListenTimeMs { get; init; }
}

public record ListenSummary
{
    public required int TotalListens { get; init; }
    public required long TotalListenTimeMs { get; init; }
    public required int UniquePodcasts { get; init; }
    public required int UniqueEpisodes { get; init; }
}
