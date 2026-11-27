using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Persistence;

public sealed class SqliteLyricsCache : IHostedService, ILyricsCache, IDisposable
{
    private const int MaximumDecompressedPayloadBytes = 8 * 1024 * 1024;
    private const long DefaultMaximumPayloadBytes = 180L * 1024 * 1024;
    private const long MaximumDatabaseBytes = 200L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _databasePath;
    private readonly ILogger<SqliteLyricsCache> _logger;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly long _maximumPayloadBytes;
    private readonly string _connectionString;
    private bool _initialized;
    private bool _disposed;

    public SqliteLyricsCache(
        ILogger<SqliteLyricsCache> logger,
        string? databasePath = null,
        long maximumPayloadBytes = DefaultMaximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (maximumPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));
        }

        _logger = logger;
        _maximumPayloadBytes = maximumPayloadBytes;
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyLyrics",
            "lyrics-cache.db");

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LyricsCacheEntry?> GetAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetCoreAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<LyricsCacheEntry?> GetCoreAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Provider, ProviderId, Payload, CachedAtUtc
            FROM LyricsCache
            WHERE CacheKey = $cacheKey;
            """;
        command.Parameters.AddWithValue("$cacheKey", cacheKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var provider = reader.GetString(0);
        int? providerId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        var payload = (byte[])reader[2];
        var cachedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3));
        var document = await DecompressDocumentAsync(payload, cancellationToken).ConfigureAwait(false);

        await reader.DisposeAsync().ConfigureAwait(false);

        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE LyricsCache
            SET LastAccessedUtc = $lastAccessedUtc
            WHERE CacheKey = $cacheKey;
            """;
        update.Parameters.AddWithValue("$lastAccessedUtc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        update.Parameters.AddWithValue("$cacheKey", cacheKey);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return new LyricsCacheEntry(cacheKey, provider, providerId, document, cachedAt);
    }

    public async Task PutAsync(LyricsCacheEntry entry, CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PutCoreAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task PutCoreAsync(LyricsCacheEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.CacheKey);
        ArgumentNullException.ThrowIfNull(entry.Document);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Provider);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var payload = await CompressDocumentAsync(entry.Document, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LyricsCache
                (CacheKey, Provider, ProviderId, Payload, PayloadBytes, CachedAtUtc, LastAccessedUtc)
            VALUES
                ($cacheKey, $provider, $providerId, $payload, $payloadBytes, $cachedAtUtc, $lastAccessedUtc)
            ON CONFLICT(CacheKey) DO UPDATE SET
                Provider = excluded.Provider,
                ProviderId = excluded.ProviderId,
                Payload = excluded.Payload,
                PayloadBytes = excluded.PayloadBytes,
                CachedAtUtc = excluded.CachedAtUtc,
                LastAccessedUtc = excluded.LastAccessedUtc;
            """;
        command.Parameters.AddWithValue("$cacheKey", entry.CacheKey);
        command.Parameters.AddWithValue("$provider", entry.Provider);
        command.Parameters.AddWithValue("$providerId", (object?)entry.ProviderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$payloadBytes", payload.Length);
        command.Parameters.AddWithValue("$cachedAtUtc", entry.CachedAtUtc.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$lastAccessedUtc", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var evicted = await EvictLeastRecentlyUsedAsync(connection, cancellationToken).ConfigureAwait(false);
        if (evicted)
        {
            await connection.CloseAsync().ConfigureAwait(false);
            await CompactDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
    }


    private async Task<bool> EvictLeastRecentlyUsedAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var removedAny = false;

        while (true)
        {
            await using var totalCommand = connection.CreateCommand();
            totalCommand.CommandText = "SELECT COALESCE(SUM(PayloadBytes), 0) FROM LyricsCache;";
            var totalValue = await totalCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var totalBytes = Convert.ToInt64(totalValue, System.Globalization.CultureInfo.InvariantCulture);
            if (totalBytes <= _maximumPayloadBytes)
            {
                return removedAny;
            }

            await using var oldestCommand = connection.CreateCommand();
            oldestCommand.CommandText = """
                SELECT CacheKey
                FROM LyricsCache
                ORDER BY LastAccessedUtc ASC, CachedAtUtc ASC, CacheKey ASC
                LIMIT 1;
                """;
            var oldestKey = await oldestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (oldestKey is null || oldestKey is DBNull)
            {
                return removedAny;
            }

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM LyricsCache WHERE CacheKey = $cacheKey;";
            deleteCommand.Parameters.AddWithValue("$cacheKey", (string)oldestKey);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            removedAny = true;
        }
    }

    private async Task CompactDatabaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await using (var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecutePragmaAsync(
                    connection,
                    "PRAGMA wal_checkpoint(TRUNCATE);",
                    cancellationToken).ConfigureAwait(false);

                await using var vacuum = connection.CreateCommand();
                vacuum.CommandText = "VACUUM;";
                await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (GetDatabaseStorageBytes() <= MaximumDatabaseBytes)
            {
                return;
            }

            await using var cleanupConnection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var oldestCommand = cleanupConnection.CreateCommand();
            oldestCommand.CommandText = """
                SELECT CacheKey
                FROM LyricsCache
                ORDER BY LastAccessedUtc ASC, CachedAtUtc ASC, CacheKey ASC
                LIMIT 1;
                """;

            var oldestKey = await oldestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (oldestKey is null || oldestKey is DBNull)
            {
                return;
            }

            await using var deleteCommand = cleanupConnection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM LyricsCache WHERE CacheKey = $cacheKey;";
            deleteCommand.Parameters.AddWithValue("$cacheKey", (string)oldestKey);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private long GetDatabaseStorageBytes()
    {
        long total = 0;
        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                total += new FileInfo(path).Length;
            }
        }

        return total;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        await _initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA synchronous=NORMAL;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout=5000;", cancellationToken).ConfigureAwait(false);

            await using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS LyricsCache
                (
                    CacheKey TEXT NOT NULL PRIMARY KEY,
                    Provider TEXT NOT NULL,
                    ProviderId INTEGER NULL,
                    Payload BLOB NOT NULL,
                    PayloadBytes INTEGER NOT NULL,
                    CachedAtUtc INTEGER NOT NULL,
                    LastAccessedUtc INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_LyricsCache_LastAccessedUtc
                ON LyricsCache(LastAccessedUtc);
                """;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
            _logger.LogInformation("Lyrics SQLite cache initialized at {DatabasePath}.", _databasePath);
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout=5000;", cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ExecutePragmaAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static CacheDocumentDto ToCacheDocument(LyricsDocument document) =>
        new(
            document.Title,
            document.Artist,
            document.Album,
            document.Duration,
            document.Instrumental,
            document.Lines.Select(static line => new CacheLineDto(
                line.Text,
                line.Start,
                line.End,
                line.Words.Select(static word => new CacheWordDto(word.Text, word.Start, word.End)).ToArray()))
                .ToArray(),
            document.PlainText,
            document.Diagnostics.ToArray(),
            document.Language);

    private static LyricsDocument FromCacheDocument(CacheDocumentDto cached) =>
        new(
            cached.Title,
            cached.Artist,
            cached.Album,
            cached.Duration,
            cached.Instrumental,
            cached.Lines.Select(static line => new LyricLine(
                line.Text,
                line.Start,
                line.End,
                line.Words.Select(static word => new LyricWord(word.Start, word.End, word.Text)))),
            cached.PlainText,
            cached.Diagnostics,
            cached.Language);

    private static async Task<byte[]> CompressDocumentAsync(
        LyricsDocument document,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(ToCacheDocument(document), JsonOptions);
        await using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            await gzip.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static async Task<LyricsDocument> DecompressDocumentAsync(
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await using var input = new MemoryStream(payload, writable: false);
        await using var gzip = new GZipStream(input, CompressionMode.Decompress);
        await using var output = new MemoryStream();

        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await gzip.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumDecompressedPayloadBytes)
            {
                throw new InvalidDataException("Cached lyrics payload exceeds the decompression safety limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var cached = JsonSerializer.Deserialize<CacheDocumentDto>(output.ToArray(), JsonOptions)
            ?? throw new InvalidDataException("Cached lyrics document could not be deserialized.");

        return FromCacheDocument(cached);
    }

    public sealed record CacheDocumentDto(
        string? Title,
        string? Artist,
        string? Album,
        TimeSpan? Duration,
        bool Instrumental,
        CacheLineDto[] Lines,
        string? PlainText,
        string[] Diagnostics,
        string? Language);

    public sealed record CacheLineDto(
        string Text,
        TimeSpan? Start,
        TimeSpan? End,
        CacheWordDto[] Words);

    public sealed record CacheWordDto(
        string Text,
        TimeSpan Start,
        TimeSpan? End);
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initializeGate.Dispose();
        _operationGate.Dispose();
    }
}
