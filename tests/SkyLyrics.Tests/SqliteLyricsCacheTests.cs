using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Persistence;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class SqliteLyricsCacheTests
{
    [Fact]
    public async Task CacheRoundTripsCompressedLyricsDocument()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(
            NullLogger<SqliteLyricsCache>.Instance,
            database);

        await cache.StartAsync(CancellationToken.None);

        var document = new LyricsDocument(
            "Cached Track",
            "Cached Artist",
            "Cached Album",
            TimeSpan.FromSeconds(120),
            false,
            [
                new LyricLine(
                    "Hello world",
                    TimeSpan.FromMilliseconds(1200),
                    TimeSpan.FromMilliseconds(2400),
                    [
                        new LyricWord(TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(1700), "Hello "),
                        new LyricWord(TimeSpan.FromMilliseconds(1700), TimeSpan.FromMilliseconds(2400), "world")
                    ])
            ],
            "Hello world");

        var entry = new LyricsCacheEntry(
            "cache-key-01",
            "LRCLIB",
            55,
            document,
            DateTimeOffset.UtcNow);

        await cache.PutAsync(entry, CancellationToken.None);
        var actual = await cache.GetAsync("cache-key-01", CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal("LRCLIB", actual.Provider);
        Assert.Equal(55, actual.ProviderId);
        Assert.Equal(LyricsSyncMode.WordSynced, actual.Document.SyncMode);
        Assert.Equal(document.Lines[0].Words.ToArray(), actual.Document.Lines[0].Words.ToArray());
        Assert.True(File.Exists(database));
    }

    [Fact]
    public async Task UpsertReplacesExistingLyricsEntry()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(
            NullLogger<SqliteLyricsCache>.Instance,
            database);

        await cache.StartAsync(CancellationToken.None);
        await cache.PutAsync(CreateEntry("First"), CancellationToken.None);
        await cache.PutAsync(CreateEntry("Updated"), CancellationToken.None);

        var actual = await cache.GetAsync("upsert-key", CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal("Updated", actual.Document.Lines[0].Text);
    }

    [Fact]
    public async Task GetRecentReturnsMostRecentlyUsedEntriesAndHonorsLimit()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(NullLogger<SqliteLyricsCache>.Instance, database);
        await cache.StartAsync(CancellationToken.None);

        await cache.PutAsync(CreateEntry("older-key", "Older record"), CancellationToken.None);
        await Task.Delay(5);
        await cache.PutAsync(CreateEntry("newer-key", "Newer record"), CancellationToken.None);

        var recent = await cache.GetRecentAsync(1, CancellationToken.None);

        var entry = Assert.Single(recent);
        Assert.Equal("newer-key", entry.CacheKey);
        Assert.Equal("Newer record", entry.Document.Lines[0].Text);
    }

    [Fact]
    public async Task DeleteRemovesOnlyTheRequestedCacheEntry()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(NullLogger<SqliteLyricsCache>.Instance, database);
        await cache.StartAsync(CancellationToken.None);

        await cache.PutAsync(CreateEntry("delete-me", "Delete"), CancellationToken.None);
        await cache.PutAsync(CreateEntry("keep-me", "Keep"), CancellationToken.None);

        Assert.True(await cache.DeleteAsync("delete-me", CancellationToken.None));
        Assert.False(await cache.DeleteAsync("delete-me", CancellationToken.None));
        Assert.Null(await cache.GetAsync("delete-me", CancellationToken.None));
        Assert.NotNull(await cache.GetAsync("keep-me", CancellationToken.None));
    }

    [Fact]
    public async Task MissingCacheKeyReturnsNull()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(
            NullLogger<SqliteLyricsCache>.Instance,
            database);

        await cache.StartAsync(CancellationToken.None);

        Assert.Null(await cache.GetAsync("not-present", CancellationToken.None));
    }

    [Fact]
    public async Task EvictsEntriesWhenTheConfiguredCompressedPayloadBudgetIsExceeded()
    {
        var database = CreateTemporaryDatabase();
        using var cache = new SqliteLyricsCache(
            NullLogger<SqliteLyricsCache>.Instance,
            database,
            maximumPayloadBytes: 1);

        await cache.StartAsync(CancellationToken.None);
        await cache.PutAsync(CreateEntry("This payload is larger than one byte."), CancellationToken.None);

        var result = await cache.GetAsync("upsert-key", CancellationToken.None);

        Assert.Null(result);
    }

    private static LyricsCacheEntry CreateEntry(string text) =>
        CreateEntry("upsert-key", text);

    private static LyricsCacheEntry CreateEntry(string cacheKey, string text) =>
        new(
            cacheKey,
            "Test Provider",
            1,
            new LyricsDocument(
                "Track",
                "Artist",
                null,
                null,
                false,
                [new LyricLine(text, TimeSpan.FromSeconds(1))]),
            DateTimeOffset.UtcNow);

    private static string CreateTemporaryDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "SkyLyricsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "lyrics.db");
    }
}
