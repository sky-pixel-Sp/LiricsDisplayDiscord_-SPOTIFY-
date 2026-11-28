using System.Text;
using System.Text.RegularExpressions;

namespace SkyLyrics.Application.Lyrics;

public sealed record LyricsMatchValidationResult(
    bool IsMatch,
    bool TitleMatches,
    bool ArtistMatches,
    bool ArtistMatchedPartially,
    bool DurationMatches,
    double? DurationDifferenceSeconds,
    string Reason);

public static class LyricsMatchValidator
{
    private const double DurationToleranceSeconds = 3d;

    private static readonly Regex FeaturingTagRegex = new(
        @"[\(\[]\s*(?:feat(?:uring)?|ft)\.?\s+[^)\]]*[)\]]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RemasterTagRegex = new(
        @"(?:\s*[-–—]\s*|\s+)?(?:\(\s*|\[\s*)?remaster(?:ed)?(?:\s+(?:19|20)\d{2})?\s*(?:\)|\])?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VersionTagRegex = new(
        @"[\(\[]\s*(?:versions?|live)\s*[)\]]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ArtistCreditSeparatorRegex = new(
        @"\s*(?:,|&|;|\+|/|\bfeat(?:uring)?\.?\b|\bft\.?\b|\bwith\b|\band\b|\bx\b)\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static LyricsMatchValidationResult Validate(
        LyricsTrackSignature signature,
        SkyLyrics.Core.Lyrics.LyricsDocument document)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(document);

        var expectedTitle = NormalizeTitle(signature.Title);
        var actualTitle = NormalizeTitle(document.Title);
        var titleMatches = expectedTitle.Length > 0 &&
                           actualTitle.Length > 0 &&
                           StringComparer.Ordinal.Equals(expectedTitle, actualTitle);

        var artistMatches = ArtistsMatch(
            signature.Artist,
            document.Artist,
            out var artistMatchedPartially);

        double? durationDifference = null;
        var durationMatches = false;
        if (signature.Duration is { } expectedDuration &&
            document.Duration is { } actualDuration &&
            expectedDuration > TimeSpan.Zero &&
            actualDuration > TimeSpan.Zero)
        {
            durationDifference = Math.Abs((expectedDuration - actualDuration).TotalSeconds);
            durationMatches = durationDifference <= DurationToleranceSeconds;
        }

        var reasons = new List<string>(3)
        {
            titleMatches ? "Judul cocok" : "Judul berbeda",
            !artistMatches
                ? "Artis berbeda"
                : artistMatchedPartially
                    ? "Artis cocok sebagian (kolaborasi)"
                    : "Artis cocok",
            durationDifference is null
                ? "Durasi tidak tersedia"
                : durationMatches
                    ? $"Durasi cocok (selisih {durationDifference.Value:0.0} dtk)"
                    : $"Durasi berbeda (selisih {durationDifference.Value:0.0} dtk)"
        };

        return new LyricsMatchValidationResult(
            titleMatches && artistMatches && durationMatches,
            titleMatches,
            artistMatches,
            artistMatchedPartially,
            durationMatches,
            durationDifference,
            string.Join(" · ", reasons));
    }

    public static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = FeaturingTagRegex.Replace(value, " ");
        normalized = RemasterTagRegex.Replace(normalized, " ");
        normalized = VersionTagRegex.Replace(normalized, " ");
        return NormalizeMetadata(normalized);
    }

    public static string NormalizeArtist(string? value) => NormalizeMetadata(value);

    public static string NormalizeMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;

        foreach (var rune in decomposed.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark
                or System.Globalization.UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                builder.Append(rune.ToString().ToLowerInvariant());
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool ArtistsMatch(
        string? expectedArtist,
        string? actualArtist,
        out bool matchedPartially)
    {
        matchedPartially = false;
        var expected = NormalizeArtist(expectedArtist);
        var actual = NormalizeArtist(actualArtist);

        if (expected.Length == 0 || actual.Length == 0)
        {
            return false;
        }

        if (StringComparer.Ordinal.Equals(expected, actual))
        {
            return true;
        }

        var expectedCredits = GetArtistCredits(expectedArtist);
        var actualCredits = GetArtistCredits(actualArtist);
        var matchesCredit = expectedCredits.Overlaps(actualCredits);

        matchedPartially = matchesCredit;
        return matchesCredit;
    }

    private static HashSet<string> GetArtistCredits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return ArtistCreditSeparatorRegex
            .Split(value)
            .Select(NormalizeArtist)
            .Where(static credit => credit.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }
}
