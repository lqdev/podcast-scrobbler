namespace PodcastScrobbler.Config;

public class ScrobblerConfig
{
    public string DatabasePath { get; set; } = "./data/scrobbles.db";
    public string? ScrobblerToken { get; set; }
    public bool RequireAuthForReads { get; set; } = false;
    public string Port { get; set; } = "5000";
}
