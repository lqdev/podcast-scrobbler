using System.Text.Json;
using DuckDB.NET.Data;
using PodcastScrobbler.Models;

namespace PodcastScrobbler.Data;

public class ListenRepository
{
    private readonly DuckDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public ListenRepository(DuckDbContext db)
    {
        _db = db;
    }

    public async Task InsertListen(Listen listen)
    {
        var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO listens (id, username, listened_at, artist_name, track_name, additional_info) VALUES ($1, $2, $3, $4, $5, $6)";
        cmd.Parameters.Add(new DuckDBParameter { Value = listen.Id.ToString() });
        cmd.Parameters.Add(new DuckDBParameter { Value = listen.Username });
        cmd.Parameters.Add(new DuckDBParameter { Value = listen.ListenedAt });
        cmd.Parameters.Add(new DuckDBParameter { Value = listen.ArtistName });
        cmd.Parameters.Add(new DuckDBParameter { Value = listen.TrackName });
        cmd.Parameters.Add(new DuckDBParameter { Value = SerializeAdditionalInfo(listen.AdditionalInfo) });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertListens(IEnumerable<Listen> listens)
    {
        foreach (var listen in listens)
        {
            await InsertListen(listen);
        }
    }

    public async Task<List<Listen>> GetListens(string username, int count, long? maxTs, long? minTs)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();

            var sql = "SELECT id, username, listened_at, artist_name, track_name, additional_info, created_at FROM listens WHERE username = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = username });

            if (maxTs.HasValue)
            {
                sql += " AND listened_at < $2";
                cmd.Parameters.Add(new DuckDBParameter { Value = maxTs.Value });
            }
            else if (minTs.HasValue)
            {
                sql += " AND listened_at > $2";
                cmd.Parameters.Add(new DuckDBParameter { Value = minTs.Value });
            }

            sql += " ORDER BY listened_at DESC LIMIT " + count;
            cmd.CommandText = sql;

            var listens = new List<Listen>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                listens.Add(ReadListen(reader));
            }
            return listens;
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task<int> GetListenCount(string username)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM listens WHERE username = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = username });
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task<List<PodcastStats>> GetTopPodcasts(string username, int limit)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT artist_name, COUNT(*) as listen_count,
                       COALESCE(SUM(CAST(json_extract(additional_info, '$.duration_ms') AS BIGINT)), 0) as total_time
                FROM listens WHERE username = $1
                GROUP BY artist_name ORDER BY listen_count DESC
                """ + $" LIMIT {limit}";
            cmd.Parameters.Add(new DuckDBParameter { Value = username });

            var stats = new List<PodcastStats>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.Add(new PodcastStats
                {
                    PodcastName = reader.GetString(0),
                    ListenCount = Convert.ToInt32(reader.GetValue(1)),
                    TotalListenTimeMs = reader.IsDBNull(2) ? 0 : long.Parse(reader.GetValue(2)!.ToString()!)
                });
            }
            return stats;
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task<List<WeeklyStats>> GetWeeklyStats(string username, int weeks)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT CAST(date_trunc('week', to_timestamp(listened_at)) AS DATE) as week_start,
                       COUNT(*) as listen_count,
                       COALESCE(SUM(CAST(json_extract(additional_info, '$.duration_ms') AS BIGINT)), 0) as total_time
                FROM listens WHERE username = $1
                  AND listened_at >= extract(epoch FROM now() - INTERVAL '{weeks} weeks')::BIGINT
                GROUP BY week_start ORDER BY week_start DESC
                """.Replace("{weeks}", weeks.ToString());
            cmd.Parameters.Add(new DuckDBParameter { Value = username });

            var stats = new List<WeeklyStats>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.Add(new WeeklyStats
                {
                    WeekStart = reader.GetValue(0)?.ToString() ?? "",
                    ListenCount = Convert.ToInt32(reader.GetValue(1)),
                    TotalListenTimeMs = reader.IsDBNull(2) ? 0 : long.Parse(reader.GetValue(2)!.ToString()!)
                });
            }
            return stats;
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task<List<string>> GetRecentPodcasts(string username, int limit)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT artist_name FROM listens WHERE username = $1 GROUP BY artist_name ORDER BY MAX(listened_at) DESC LIMIT {limit}";
            cmd.Parameters.Add(new DuckDBParameter { Value = username });

            var podcasts = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                podcasts.Add(reader.GetString(0));
            }
            return podcasts;
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    public async Task<ListenSummary> GetSummary(string username)
    {
        var conn = _db.GetReadConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) as total_listens,
                       COALESCE(SUM(CAST(json_extract(additional_info, '$.duration_ms') AS BIGINT)), 0) as total_time,
                       COUNT(DISTINCT artist_name) as unique_podcasts,
                       COUNT(DISTINCT track_name) as unique_episodes
                FROM listens WHERE username = $1
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = username });

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new ListenSummary
            {
                TotalListens = Convert.ToInt32(reader.GetValue(0)),
                TotalListenTimeMs = reader.IsDBNull(1) ? 0 : long.Parse(reader.GetValue(1)!.ToString()!),
                UniquePodcasts = Convert.ToInt32(reader.GetValue(2)),
                UniqueEpisodes = Convert.ToInt32(reader.GetValue(3))
            };
        }
        finally
        {
            if (!_db.IsInMemory) conn.Dispose();
        }
    }

    private static Listen ReadListen(System.Data.Common.DbDataReader reader)
    {
        var additionalInfoStr = reader.IsDBNull(5) ? null : reader.GetValue(5)?.ToString();
        return new Listen
        {
            Id = Guid.Parse(reader.GetValue(0)?.ToString() ?? Guid.NewGuid().ToString()),
            Username = reader.GetString(1),
            ListenedAt = Convert.ToInt64(reader.GetValue(2)),
            ArtistName = reader.GetString(3),
            TrackName = reader.GetString(4),
            AdditionalInfo = string.IsNullOrEmpty(additionalInfoStr)
                ? null
                : JsonSerializer.Deserialize<AdditionalInfo>(additionalInfoStr, JsonOptions),
            CreatedAt = reader.IsDBNull(6) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(reader.GetValue(6)!.ToString()!)
        };
    }

    private static string? SerializeAdditionalInfo(AdditionalInfo? info)
    {
        return info is null ? null : JsonSerializer.Serialize(info, JsonOptions);
    }
}
