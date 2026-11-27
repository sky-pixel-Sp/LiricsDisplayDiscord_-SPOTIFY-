using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LyricsServiceTests
{
    [Fact]
    public async Task FirstLookupQueriesProviderAndCachesReturnedLyrics()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache();
        var service = CreateService(provider, cache);

        var result = await service.GetExactAsync(
            new LyricsTrackSignature("Track", "Artist", "Album", TimeSpan.FromSeconds(100)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LyricsLookupSource.Provider, result.Source);
        Assert.Equal(1, provider.ExactCalls);
        Assert.Single(cache.Entries);
    }

    [Fact]
    public async Task SecondLookupReturnsCachedResultWithoutCallingProvider()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache();
        var service = CreateService(provider, cache);
        var signature = new LyricsTrackSignature("Track", "Artist", "Album", TimeSpan.FromSeconds(100));

        await service.GetExactAsync(signature, CancellationToken.None);
        var second = await service.GetExactAsync(signature, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(LyricsLookupSource.Cache, second.Source);
        Assert.Equal(1, provider.ExactCalls);
    }

    [Fact]
    public async Task CacheFailureDoesNotBlockProviderLookup()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache { ThrowOnGet = true };
        var service = CreateService(provider, cache);

        var result = await service.GetExactAsync(
            new LyricsTrackSignature("Track", "Artist", null, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, provider.ExactCalls);
    }

    private static LyricsService CreateService(FakeProvider provider, FakeCache cache) =>
        new(
            provider,
            cache,
            new LocalLyricsLoader(new LrcParser(), new LyricsFileParser()),
            NullLogger<LyricsService>.Instance,
            TimeProvider.System);

    private static LyricsProviderResult CreateProviderResult(string text) =>
        new(
            "Test provider",
            7,
            new LyricsDocument("Track", "Artist", "Album", TimeSpan.FromSeconds(100), false,
                [new LyricLine(text, TimeSpan.FromSeconds(1))]));

    private sealed class FakeProvider(LyricsProviderResult? result) : ILyricsProvider
    {
        public string Name => "Test provider";
        public int ExactCalls { get; private set; }

        public Task<LyricsProviderResult?> GetExactAsync(
            LyricsTrackSignature signature,
            CancellationToken cancellationToken)
        {
            ExactCalls++;
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<LyricsProviderResult>> SearchAsync(
            LyricsTrackSignature signature,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LyricsProviderResult>>([]);

        public Task<LyricsProviderResult?> GetByIdAsync(
            int providerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeCache : ILyricsCache
    {
        public Dictionary<string, LyricsCacheEntry> Entries { get; } = [];
        public bool ThrowOnGet { get; init; }

        public Task<LyricsCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken)
        {
            if (ThrowOnGet)
            {
                throw new IOException("Simulated cache failure.");
            }

            Entries.TryGetValue(cacheKey, out var entry);
            return Task.FromResult(entry);
        }

        public Task PutAsync(LyricsCacheEntry entry, CancellationToken cancellationToken)
        {
            Entries[entry.CacheKey] = entry;
            return Task.CompletedTask;
        }
    }
}
