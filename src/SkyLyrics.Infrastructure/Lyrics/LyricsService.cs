using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LyricsService : ILyricsService
{
    private readonly ILyricsProvider _provider;
    private readonly ILyricsCache _cache;
    private readonly LocalLyricsLoader _localLyricsLoader;
    private readonly ILogger<LyricsService> _logger;
    private readonly TimeProvider _timeProvider;

    public LyricsService(
        ILyricsProvider provider,
        ILyricsCache cache,
        LocalLyricsLoader localLyricsLoader,
        ILogger<LyricsService> logger,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _cache = cache;
        _localLyricsLoader = localLyricsLoader;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<LyricsLookupResult?> GetExactAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature.Artist);

        var cacheKey = LyricsTrackSignature.CreateCacheKey(signature);

        try
        {
            var cached = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return new LyricsLookupResult(
                    cached.Document,
                    LyricsLookupSource.Cache,
                    cached.Provider,
                    cached.ProviderId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Lyrics cache lookup failed; the provider will be queried.");
        }

        var result = await _provider.GetExactAsync(signature, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return null;
        }

        var entry = new LyricsCacheEntry(
            cacheKey,
            result.Provider,
            result.ProviderId,
            result.Document,
            _timeProvider.GetUtcNow());

        try
        {
            await _cache.PutAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Lyrics were retrieved but could not be written to the local cache.");
        }

        return new LyricsLookupResult(
            result.Document,
            LyricsLookupSource.Provider,
            result.Provider,
            result.ProviderId);
    }

    public async Task<IReadOnlyList<LyricsSearchCandidate>> SearchAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken)
    {
        var candidates = await _provider.SearchAsync(signature, cancellationToken).ConfigureAwait(false);
        return LyricsCandidateRanker.Rank(signature, candidates);
    }

    public async Task<LyricsLookupResult> LoadLocalFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var document = await _localLyricsLoader.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(document.Title) &&
            !string.IsNullOrWhiteSpace(document.Artist))
        {
            var signature = new LyricsTrackSignature(
                document.Title,
                document.Artist,
                document.Album,
                document.Duration);

            var entry = new LyricsCacheEntry(
                LyricsTrackSignature.CreateCacheKey(signature),
                "Local file",
                null,
                document,
                _timeProvider.GetUtcNow());

            try
            {
                await _cache.PutAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "The imported lyrics file was loaded but could not be cached.");
            }
        }

        return new LyricsLookupResult(
            document,
            LyricsLookupSource.LocalFile,
            Path.GetFileName(filePath),
            null);
    }
}
