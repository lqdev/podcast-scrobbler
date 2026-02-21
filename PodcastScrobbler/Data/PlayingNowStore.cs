using System.Collections.Concurrent;
using System.Text.Json;
using DuckDB.NET.Data;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Data;

public class PlayingNowStore
{
    private readonly ConcurrentDictionary<string, PlayingNow> _store = new();
    private readonly DuckDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public PlayingNowStore(DuckDbContext db)
    {
        _db = db;
    }

    public async Task LoadFromDb()
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT username, updated_at, artist_name, track_name, additional_info FROM playing_now";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var additionalInfoStr = reader.IsDBNull(4) ? null : reader.GetValue(4)?.ToString();
                var playingNow = new PlayingNow
                {
                    Username = reader.GetString(0),
                    UpdatedAt = DateTimeOffset.Parse(reader.GetValue(1)!.ToString()!),
                    ArtistName = reader.GetString(2),
                    TrackName = reader.GetString(3),
                    AdditionalInfo = string.IsNullOrEmpty(additionalInfoStr)
                        ? null
                        : JsonSerializer.Deserialize<AdditionalInfo>(additionalInfoStr, JsonOptions)
                };
                _store[playingNow.Username] = playingNow;
            }
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task Set(PlayingNow playingNow)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO playing_now (username, artist_name, track_name, additional_info)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (username) DO UPDATE SET
                artist_name = EXCLUDED.artist_name,
                track_name = EXCLUDED.track_name,
                additional_info = EXCLUDED.additional_info,
                updated_at = now()
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = playingNow.Username });
        cmd.Parameters.Add(new DuckDBParameter { Value = playingNow.ArtistName });
        cmd.Parameters.Add(new DuckDBParameter { Value = playingNow.TrackName });
        cmd.Parameters.Add(new DuckDBParameter { Value = playingNow.AdditionalInfo is null ? null : JsonSerializer.Serialize(playingNow.AdditionalInfo, JsonOptions) });
        await cmd.ExecuteNonQueryAsync();

        _store[playingNow.Username] = playingNow;
    }

    public PlayingNow? Get(string username)
    {
        _store.TryGetValue(username, out var playingNow);
        return playingNow;
    }
}
