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
                var validation = LyricsMatchValidator.Validate(signature, cached.Document);
                if (validation.IsMatch)
                {
                    return new LyricsLookupResult(
                        cached.Document,
                        LyricsLookupSource.Cache,
                        cached.Provider,
                        cached.ProviderId);
                }

                _logger.LogWarning(
                    "Ignoring cached lyrics for {TrackTitle}: {ValidationReason}.",
                    signature.Title,
                    validation.Reason);

                if (_cache is ILyricsLibrary library)
                {
                    try
                    {
                        await library.DeleteAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "Invalid cached lyrics could not be deleted.");
                    }
                }
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

        var providerValidation = LyricsMatchValidator.Validate(signature, result.Document);
        if (!providerValidation.IsMatch)
        {
            _logger.LogWarning(
                "Rejecting LRCLIB exact result for {TrackTitle}: {ValidationReason}.",
                signature.Title,
                providerValidation.Reason);
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

    public async Task<LyricsLookupResult?> SelectCandidateAsync(
        LyricsTrackSignature signature,
        LyricsSearchCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(candidate);

        var validation = LyricsMatchValidator.Validate(signature, candidate.Result.Document);
        if (!validation.IsMatch)
        {
            _logger.LogWarning(
                "Rejecting manually selected lyrics candidate for {TrackTitle}: {ValidationReason}.",
                signature.Title,
                validation.Reason);
            return null;
        }

        var cacheKey = LyricsTrackSignature.CreateCacheKey(signature);
        var entry = new LyricsCacheEntry(
            cacheKey,
            candidate.Result.Provider,
            candidate.Result.ProviderId,
            candidate.Result.Document,
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
            _logger.LogWarning(exception, "The selected lyrics candidate could not be cached.");
        }

        return new LyricsLookupResult(
            candidate.Result.Document,
            LyricsLookupSource.Provider,
            candidate.Result.Provider,
            candidate.Result.ProviderId);
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
