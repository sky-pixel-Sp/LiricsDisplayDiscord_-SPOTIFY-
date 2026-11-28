namespace SkyLyrics.Application.Lyrics;

public static class LyricsCandidateRanker
{
    public static IReadOnlyList<LyricsSearchCandidate> Rank(
        LyricsTrackSignature signature,
        IEnumerable<LyricsProviderResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(candidates);

        var normalizedTitle = LyricsMatchValidator.NormalizeTitle(signature.Title);
        var normalizedArtist = LyricsMatchValidator.NormalizeArtist(signature.Artist);
        var normalizedAlbum = LyricsMatchValidator.NormalizeMetadata(signature.Album);

        return candidates
            .Select(candidate =>
            {
                ArgumentNullException.ThrowIfNull(candidate);
                var validation = LyricsMatchValidator.Validate(signature, candidate.Document);
                var title = LyricsMatchValidator.NormalizeTitle(candidate.Document.Title);
                var artist = LyricsMatchValidator.NormalizeArtist(candidate.Document.Artist);
                var album = LyricsMatchValidator.NormalizeMetadata(candidate.Document.Album);

                var titleExact = title.Length > 0 &&
                                 StringComparer.Ordinal.Equals(normalizedTitle, title);
                var artistExact = artist.Length > 0 &&
                                  StringComparer.Ordinal.Equals(normalizedArtist, artist);
                var albumExact = normalizedAlbum.Length > 0 &&
                                 album.Length > 0 &&
                                 StringComparer.Ordinal.Equals(normalizedAlbum, album);

                var score = titleExact
                    ? 50
                    : SimilarityScore(normalizedTitle, title) / 2;

                score += artistExact
                    ? 30
                    : validation.ArtistMatches
                        ? 24
                        : SimilarityScore(normalizedArtist, artist) / 3;

                if (albumExact)
                {
                    score += 10;
                }

                if (validation.DurationMatches)
                {
                    score += 10;
                }

                return new LyricsSearchCandidate(
                    candidate,
                    Math.Clamp(score, 0, 100),
                    validation.IsMatch)
                {
                    MatchReason = validation.Reason
                };
            })
            .OrderByDescending(static item => item.IsExactSignatureMatch)
            .ThenByDescending(static item => item.MatchScore)
            .ThenBy(static item => item.Result.ProviderId ?? int.MaxValue)
            .ToArray();
    }

    private static int SimilarityScore(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var distance = LevenshteinDistance(left, right);
        var denominator = Math.Max(left.Length, right.Length);
        return (int)Math.Round((1d - (double)distance / denominator) * 100d);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
