using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Application.Lyrics;

public sealed record LyricsTrackSignature(
    string Title,
    string Artist,
    string? Album,
    TimeSpan? Duration)
{
    public string NormalizedTitle => LyricsCacheKey.Normalize(Title);
    public string NormalizedArtist => LyricsCacheKey.Normalize(Artist);
    public string NormalizedAlbum => LyricsCacheKey.Normalize(Album);

    public static string CreateCacheKey(LyricsTrackSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var duration = signature.Duration is { } value
            ? Math.Round(value.TotalSeconds, MidpointRounding.AwayFromZero).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "unknown";

        var input = string.Join(
            "\u001f",
            signature.NormalizedTitle,
            signature.NormalizedArtist,
            signature.NormalizedAlbum,
            duration);

        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class LyricsCacheKey
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            value.Trim().ToLowerInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record LyricsProviderResult(
    string Provider,
    int? ProviderId,
    LyricsDocument Document);

public sealed record LyricsCacheEntry(
    string CacheKey,
    string Provider,
    int? ProviderId,
    LyricsDocument Document,
    DateTimeOffset CachedAtUtc);

public enum LyricsLookupSource
{
    Cache = 0,
    Provider = 1,
    LocalFile = 2
}

public sealed record LyricsLookupResult(
    LyricsDocument Document,
    LyricsLookupSource Source,
    string SourceName,
    int? ProviderId);

public sealed record LyricsSearchCandidate(
    LyricsProviderResult Result,
    int MatchScore,
    bool IsExactSignatureMatch);

public interface ILyricsProvider
{
    string Name { get; }

    Task<LyricsProviderResult?> GetExactAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LyricsProviderResult>> SearchAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken);

    Task<LyricsProviderResult?> GetByIdAsync(
        int providerId,
        CancellationToken cancellationToken);
}

public interface ILyricsCache
{
    Task<LyricsCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken);

    Task PutAsync(LyricsCacheEntry entry, CancellationToken cancellationToken);
}

public interface ILyricsLibrary
{
    Task<IReadOnlyList<LyricsCacheEntry>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string cacheKey, CancellationToken cancellationToken);
}

public interface ILyricsService
{
    Task<LyricsLookupResult?> GetExactAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LyricsSearchCandidate>> SearchAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken);

    Task<LyricsLookupResult> LoadLocalFileAsync(
        string filePath,
        CancellationToken cancellationToken);
}
