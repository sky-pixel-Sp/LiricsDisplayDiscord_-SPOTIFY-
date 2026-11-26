using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Persistence;

/// <summary>
/// Local-only cache for verified lyric documents. Each operation uses its own
/// SQLite connection; the database is stored in the user's LocalAppData folder.
/// </summary>
public sealed class SqliteLyricsCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public SqliteLyricsCache(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                CREATE TABLE IF NOT EXISTS LyricsCache (
                    TrackKey TEXT PRIMARY KEY NOT NULL,
                    TrackTitle TEXT NOT NULL,
                    ArtistName TEXT NOT NULL,
                    AlbumName TEXT NULL,
                    LyricsJson TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    CachedAtUtc TEXT NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_LyricsCache_ExpiresAtUtc
                    ON LyricsCache (ExpiresAtUtc);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task<LyricsDocument?> GetAsync(
        SpotifyTrackSnapshot track,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var key = CreateTrackKey(track);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT LyricsJson
            FROM LyricsCache
            WHERE TrackKey = $trackKey AND ExpiresAtUtc > $nowUtc
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$trackKey", key);
        command.Parameters.AddWithValue("$nowUtc", DateTimeOffset.UtcNow.ToString("O"));

        var stored = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        try
        {
            return JsonSerializer.Deserialize<LyricsDocument>(stored, JsonOptions);
        }
        catch (JsonException)
        {
            // A corrupt row should not permanently block a fresh provider lookup.
            await DeleteByKeyAsync(connection, key, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    public async Task SetAsync(
        SpotifyTrackSnapshot track,
        LyricsDocument lyrics,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lyrics);
        if (lyrics.Lines.Count == 0)
            throw new ArgumentException("An empty lyrics document cannot be cached.", nameof(lyrics));

        var cacheLifetime = lifetime ?? TimeSpan.FromDays(30);
        if (cacheLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Cache lifetime must be positive.");

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var serialized = JsonSerializer.Serialize(lyrics, JsonOptions);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LyricsCache
                (TrackKey, TrackTitle, ArtistName, AlbumName, LyricsJson, Source, CachedAtUtc, ExpiresAtUtc)
            VALUES
                ($trackKey, $trackTitle, $artistName, $albumName, $lyricsJson, $source, $cachedAtUtc, $expiresAtUtc)
            ON CONFLICT(TrackKey) DO UPDATE SET
                TrackTitle = excluded.TrackTitle,
                ArtistName = excluded.ArtistName,
                AlbumName = excluded.AlbumName,
                LyricsJson = excluded.LyricsJson,
                Source = excluded.Source,
                CachedAtUtc = excluded.CachedAtUtc,
                ExpiresAtUtc = excluded.ExpiresAtUtc;
            """;
        command.Parameters.AddWithValue("$trackKey", CreateTrackKey(track));
        command.Parameters.AddWithValue("$trackTitle", track.Title);
        command.Parameters.AddWithValue("$artistName", track.Artist);
        command.Parameters.AddWithValue("$albumName", (object?)track.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$lyricsJson", serialized);
        command.Parameters.AddWithValue("$source", lyrics.Source);
        command.Parameters.AddWithValue("$cachedAtUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$expiresAtUtc", now.Add(cacheLifetime).ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM LyricsCache WHERE ExpiresAtUtc <= $nowUtc;";
        command.Parameters.AddWithValue("$nowUtc", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DeleteByKeyAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM LyricsCache WHERE TrackKey = $trackKey;";
        delete.Parameters.AddWithValue("$trackKey", key);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CreateTrackKey(SpotifyTrackSnapshot track)
    {
        static string Normalize(string? value) =>
            string.Join(' ', (value ?? string.Empty)
                .Normalize(NormalizationForm.FormKC)
                .Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();

        var identity = $"{Normalize(track.Artist)}\u001f{Normalize(track.Title)}\u001f{Normalize(track.Album)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
