using System.Text;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Core.Lyrics;

public sealed record LyricsMatchResult(bool IsMatch, string Reason);

/// <summary>
/// Verifies that an LRCLIB response belongs to the current Spotify track.
/// This is deliberately conservative: uncertain matches are not displayed.
/// </summary>
public static class LyricsMatchValidator
{
    public static LyricsMatchResult Evaluate(
        SpotifyTrackSnapshot track,
        LrclibLyrics lyrics,
        double durationToleranceSeconds = 2.0)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lyrics);

        if (durationToleranceSeconds < 0 || double.IsNaN(durationToleranceSeconds))
            throw new ArgumentOutOfRangeException(nameof(durationToleranceSeconds));

        var expectedTitle = Normalize(track.Title);
        var actualTitle = Normalize(lyrics.TrackName);
        if (expectedTitle.Length == 0 || actualTitle.Length == 0 ||
            !string.Equals(expectedTitle, actualTitle, StringComparison.Ordinal))
        {
            return new LyricsMatchResult(false, "Track title does not match the current Spotify track.");
        }

        var expectedArtist = Normalize(track.Artist);
        var actualArtist = Normalize(lyrics.ArtistName);
        if (expectedArtist.Length == 0 || actualArtist.Length == 0 ||
            !string.Equals(expectedArtist, actualArtist, StringComparison.Ordinal))
        {
            return new LyricsMatchResult(false, "Artist name does not match the current Spotify track.");
        }

        var expectedAlbum = Normalize(track.Album);
        var actualAlbum = Normalize(lyrics.AlbumName);
        if (expectedAlbum.Length > 0 && actualAlbum.Length > 0 &&
            !string.Equals(expectedAlbum, actualAlbum, StringComparison.Ordinal))
        {
            return new LyricsMatchResult(false, "Album name does not match the current Spotify track.");
        }

        if (track.Duration is { } expectedDuration &&
            expectedDuration > TimeSpan.Zero &&
            lyrics.Duration > 0)
        {
            var difference = Math.Abs(expectedDuration.TotalSeconds - lyrics.Duration);
            if (difference > durationToleranceSeconds)
            {
                return new LyricsMatchResult(
                    false,
                    $"Track duration differs by {difference:F2} seconds; allowed tolerance is {durationToleranceSeconds:F2} seconds.");
            }
        }

        return new LyricsMatchResult(true, "LRCLIB metadata matches the current Spotify track.");
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
