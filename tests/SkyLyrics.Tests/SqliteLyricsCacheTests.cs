using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Models;
using SkyLyrics.Persistence;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class SqliteLyricsCacheTests
{
    [TestMethod]
    public async Task StoresAndRetrievesLyricsIncludingWordTimestamps()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"skylyrics-cache-{Guid.NewGuid():N}.db");
        try
        {
            var cache = new SqliteLyricsCache(databasePath);
            var track = CreateTrack();
            var document = new LyricsDocument(
                new[]
                {
                    new LyricLine(
                        "Hello there",
                        TimeSpan.FromSeconds(1),
                        new[]
                        {
                            new LyricWord("Hello ", TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1400)),
                            new LyricWord("there", TimeSpan.FromMilliseconds(1450), TimeSpan.FromMilliseconds(1800))
                        })
                },
                "Sample",
                "Artist",
                true,
                "LRCLIB Lyricsfile");

            await cache.SetAsync(track, document);
            var restored = await cache.GetAsync(track);

            Assert.IsNotNull(restored);
            Assert.AreEqual("Sample", restored.Title);
            Assert.AreEqual("Artist", restored.Artist);
            Assert.IsTrue(restored.HasWordTimestamps);
            Assert.IsTrue(restored.HasLineTimestamps);
            Assert.AreEqual(2, restored.Lines[0].Words!.Count);
            Assert.AreEqual("there", restored.Lines[0].Words[1].Text);
            Assert.AreEqual(TimeSpan.FromMilliseconds(1450), restored.Lines[0].Words[1].StartTime);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [TestMethod]
    public async Task CacheKeySeparatesDifferentTracks()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"skylyrics-cache-{Guid.NewGuid():N}.db");
        try
        {
            var cache = new SqliteLyricsCache(databasePath);
            var track = CreateTrack();
            var document = LyricsDocument.FromPlainText("Cached words", "Sample", "Artist");
            var otherTrack = track with { Title = "A different track" };

            await cache.SetAsync(track, document);
            var found = await cache.GetAsync(otherTrack);

            Assert.IsNull(found);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }


    [TestMethod]
    public async Task CacheKeySeparatesDifferentTrackDurations()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"skylyrics-cache-{Guid.NewGuid():N}.db");
        try
        {
            var cache = new SqliteLyricsCache(databasePath);
            var track = CreateTrack();
            var document = LyricsDocument.FromPlainText("Cached words", "Sample", "Artist");
            var otherVersion = track with { Duration = TimeSpan.FromSeconds(198) };

            await cache.SetAsync(track, document);

            Assert.IsNull(await cache.GetAsync(otherVersion));
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [TestMethod]
    public async Task RejectsEmptyDocumentsInsteadOfCreatingEmptyCacheEntries()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"skylyrics-cache-{Guid.NewGuid():N}.db");
        try
        {
            var cache = new SqliteLyricsCache(databasePath);
            var empty = new LyricsDocument(Array.Empty<LyricLine>());
            var rejected = false;

            try
            {
                await cache.SetAsync(CreateTrack(), empty);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
            Assert.IsNull(await cache.GetAsync(CreateTrack()));
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static SpotifyTrackSnapshot CreateTrack() =>
        new("Sample", "Artist", "Album", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(180), true, "Spotify");

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var file in new[] { path, path + "-shm", path + "-wal" })
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (IOException)
            {
                // SQLite may still have a pooled connection while test cleanup runs.
            }
        }
    }
}
