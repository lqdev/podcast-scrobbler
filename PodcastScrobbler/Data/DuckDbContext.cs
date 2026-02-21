using DuckDB.NET.Data;
using PodcastScrobbler.Config;

namespace PodcastScrobbler.Data;

public class DuckDbContext : IDisposable
{
    private readonly DuckDBConnection _writeConnection;
    private readonly string _connectionString;
    private readonly bool _isInMemory;

    public DuckDbContext(ScrobblerConfig config)
    {
        var dbPath = config.DatabasePath;
        _isInMemory = dbPath == ":memory:";

        if (!_isInMemory)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={dbPath}";
        _writeConnection = new DuckDBConnection(_connectionString);
        _writeConnection.Open();
    }

    public async Task Initialize()
    {
        using var cmd = _writeConnection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS listens (
                id            UUID DEFAULT gen_random_uuid() PRIMARY KEY,
                username      VARCHAR NOT NULL,
                listened_at   BIGINT NOT NULL,
                artist_name   VARCHAR NOT NULL,
                track_name    VARCHAR NOT NULL,
                additional_info JSON,
                created_at    TIMESTAMPTZ DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS idx_listens_user_time ON listens (username, listened_at DESC);

            CREATE TABLE IF NOT EXISTS playing_now (
                username      VARCHAR PRIMARY KEY,
                updated_at    TIMESTAMPTZ DEFAULT now(),
                artist_name   VARCHAR NOT NULL,
                track_name    VARCHAR NOT NULL,
                additional_info JSON
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public DuckDBConnection GetConnection() => _writeConnection;

    /// <summary>
    /// Returns a read connection. For in-memory databases, returns the write connection
    /// since each in-memory connection is a separate database instance.
    /// Callers should only Dispose connections from file-backed databases.
    /// </summary>
    public DuckDBConnection GetReadConnection()
    {
        if (_isInMemory)
            return _writeConnection;

        var conn = new DuckDBConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public bool IsInMemory => _isInMemory;

    public void Dispose()
    {
        _writeConnection.Dispose();
        GC.SuppressFinalize(this);
    }
}
