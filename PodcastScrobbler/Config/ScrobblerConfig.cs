namespace PodcastScrobbler.Config;

public class ScrobblerConfig
{
    public string DatabasePath { get; set; } =
        Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./data/scrobbles.db";

    public string? ScrobblerToken { get; set; } =
        Environment.GetEnvironmentVariable("SCROBBLER_TOKEN");

    public bool RequireAuthForReads { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("SCROBBLER_REQUIRE_AUTH_FOR_READS"), "true", StringComparison.OrdinalIgnoreCase);

    public string Port { get; set; } =
        Environment.GetEnvironmentVariable("PORT") ?? "5000";
}
