using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LyricsServiceTests
{
    [Fact]
    public async Task FirstLookupQueriesProviderAndCachesVerifiedLyrics()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache();
        var service = CreateService(provider, cache);

        var result = await service.GetExactAsync(
            CreateSignature(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LyricsLookupSource.Provider, result.Source);
        Assert.Equal(1, provider.ExactCalls);
        Assert.Single(cache.Entries);
    }

    [Fact]
    public async Task SecondLookupReturnsValidatedCachedResultWithoutCallingProvider()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache();
        var service = CreateService(provider, cache);
        var signature = CreateSignature();

        await service.GetExactAsync(signature, CancellationToken.None);
        var second = await service.GetExactAsync(signature, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(LyricsLookupSource.Cache, second.Source);
        Assert.Equal(1, provider.ExactCalls);
    }

    [Fact]
    public async Task InvalidLegacyCacheEntryIsDeletedAndProviderIsQueried()
    {
        var signature = CreateSignature();
        var cacheKey = LyricsTrackSignature.CreateCacheKey(signature);
        var cache = new FakeCache();
        cache.Entries[cacheKey] = new LyricsCacheEntry(
            cacheKey,
            "LRCLIB",
            4,
            new LyricsDocument(
                "Different Song",
                "Example Artist",
                null,
                TimeSpan.FromSeconds(100),
                false,
                [new LyricLine("Wrong song lyrics", TimeSpan.FromSeconds(1))]),
            DateTimeOffset.UtcNow);

        var provider = new FakeProvider(CreateProviderResult("Verified line"));
        var service = CreateService(provider, cache);

        var result = await service.GetExactAsync(signature, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LyricsLookupSource.Provider, result.Source);
        Assert.Contains(cacheKey, cache.DeletedKeys);
        Assert.Equal("Verified line", result.Document.Lines[0].Text);
    }

    [Fact]
    public async Task ProviderRecordThatFailsValidationIsNeverReturnedOrCached()
    {
        var invalid = new LyricsProviderResult(
            "LRCLIB",
            91,
            new LyricsDocument(
                "Night Drives",
                "Example Artist",
                null,
                TimeSpan.FromSeconds(100),
                false,
                [new LyricLine("Lyrics for another song", TimeSpan.FromSeconds(1))]));
        var cache = new FakeCache();
        var service = CreateService(new FakeProvider(invalid), cache);

        var result = await service.GetExactAsync(CreateSignature(), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public async Task CacheFailureDoesNotBlockProviderLookup()
    {
        var provider = new FakeProvider(CreateProviderResult("line"));
        var cache = new FakeCache { ThrowOnGet = true };
        var service = CreateService(provider, cache);

        var result = await service.GetExactAsync(
            CreateSignature(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LyricsLookupSource.Provider, result.Source);
        Assert.Equal(1, provider.ExactCalls);
    }

    [Fact]
    public async Task SearchAsyncReturnsRankedCandidatesWithValidationReasons()
    {
        var matching = CreateProviderResult("Matching line");
        var similar = new LyricsProviderResult(
            "LRCLIB",
            14,
            new LyricsDocument(
                "Night Drives",
                "Example Artist",
                null,
                TimeSpan.FromSeconds(100),
                false,
                [new LyricLine("Similar title lyrics", TimeSpan.FromSeconds(1))]));
        var provider = new FakeProvider(null, [similar, matching]);
        var service = CreateService(provider, new FakeCache());

        var candidates = await service.SearchAsync(CreateSignature(), CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(7, candidates[0].Result.ProviderId);
        Assert.True(candidates[0].IsExactSignatureMatch);
        Assert.Contains("Judul cocok", candidates[0].MatchReason);
        Assert.False(candidates[1].IsExactSignatureMatch);
        Assert.Contains("Judul berbeda", candidates[1].MatchReason);
    }

    [Fact]
    public async Task ManuallySelectedCandidateIsRevalidatedAndCached()
    {
        var cache = new FakeCache();
        var service = CreateService(new FakeProvider(null), cache);
        var signature = CreateSignature();
        var candidate = new LyricsSearchCandidate(
            CreateProviderResult("Selected line"),
            100,
            true)
        {
            MatchReason = "Judul cocok · Artis cocok · Durasi cocok"
        };

        var result = await service.SelectCandidateAsync(
            signature,
            candidate,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Selected line", result.Document.Lines[0].Text);
        Assert.Single(cache.Entries);
    }

    [Fact]
    public async Task ManuallySelectedCandidateThatFailsValidationIsRejected()
    {
        var cache = new FakeCache();
        var service = CreateService(new FakeProvider(null), cache);
        var signature = CreateSignature();
        var candidate = new LyricsSearchCandidate(
            new LyricsProviderResult(
                "LRCLIB",
                92,
                new LyricsDocument(
                    "Night Drives",
                    "Example Artist",
                    null,
                    TimeSpan.FromSeconds(212),
                    false,
                    [new LyricLine("Not matching lyrics", TimeSpan.FromSeconds(1))])),
            75,
            false);

        var result = await service.SelectCandidateAsync(
            signature,
            candidate,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(cache.Entries);
    }

    private static LyricsService CreateService(FakeProvider provider, FakeCache cache) =>
        new(
            provider,
            cache,
            new LocalLyricsLoader(new LrcParser(), new LyricsFileParser()),
            NullLogger<LyricsService>.Instance,
            TimeProvider.System);

    private static LyricsTrackSignature CreateSignature() =>
        new("Night Drive", "Example Artist", null, TimeSpan.FromSeconds(100));

    private static LyricsProviderResult CreateProviderResult(string text) =>
        new(
            "LRCLIB",
            7,
            new LyricsDocument(
                "Night Drive",
                "Example Artist",
                null,
                TimeSpan.FromSeconds(100),
                false,
                [new LyricLine(text, TimeSpan.FromSeconds(1))]));

    private sealed class FakeProvider : ILyricsProvider
    {
        private readonly LyricsProviderResult? _result;
        private readonly IReadOnlyList<LyricsProviderResult> _searchResults;

        public FakeProvider(
            LyricsProviderResult? result,
            IReadOnlyList<LyricsProviderResult>? searchResults = null)
        {
            _result = result;
            _searchResults = searchResults ?? [];
        }

        public string Name => "LRCLIB";
        public int ExactCalls { get; private set; }

        public Task<LyricsProviderResult?> GetExactAsync(
            LyricsTrackSignature signature,
            CancellationToken cancellationToken)
        {
            ExactCalls++;
            return Task.FromResult(_result);
        }

        public Task<IReadOnlyList<LyricsProviderResult>> SearchAsync(
            LyricsTrackSignature signature,
            CancellationToken cancellationToken) =>
            Task.FromResult(_searchResults);

        public Task<LyricsProviderResult?> GetByIdAsync(
            int providerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private sealed class FakeCache : ILyricsCache, ILyricsLibrary
    {
        public Dictionary<string, LyricsCacheEntry> Entries { get; } = [];
        public List<string> DeletedKeys { get; } = [];
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

        public Task<IReadOnlyList<LyricsCacheEntry>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LyricsCacheEntry>>(
                Entries.Values.OrderByDescending(static entry => entry.CachedAtUtc).Take(limit).ToArray());

        public Task<bool> DeleteAsync(string cacheKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(cacheKey);
            return Task.FromResult(Entries.Remove(cacheKey));
        }
    }
}
