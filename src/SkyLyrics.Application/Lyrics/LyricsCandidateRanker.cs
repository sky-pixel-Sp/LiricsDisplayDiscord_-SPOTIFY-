namespace SkyLyrics.Application.Lyrics;

public static class LyricsCandidateRanker
{
    public static IReadOnlyList<LyricsSearchCandidate> Rank(
        LyricsTrackSignature signature,
        IEnumerable<LyricsProviderResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(candidate =>
            {
                var title = LyricsCacheKey.Normalize(candidate.Document.Title);
                var artist = LyricsCacheKey.Normalize(candidate.Document.Artist);
                var album = LyricsCacheKey.Normalize(candidate.Document.Album);

                var titleExact = title.Length > 0 && title == signature.NormalizedTitle;
                var artistExact = artist.Length > 0 && artist == signature.NormalizedArtist;
                var albumExact = signature.NormalizedAlbum.Length > 0 &&
                                 album.Length > 0 &&
                                 album == signature.NormalizedAlbum;

                var score = 0;
                if (titleExact)
                {
                    score += 50;
                }
                else
                {
                    score += SimilarityScore(signature.NormalizedTitle, title) / 2;
                }

                if (artistExact)
                {
                    score += 30;
                }
                else
                {
                    score += SimilarityScore(signature.NormalizedArtist, artist) / 3;
                }

                if (albumExact)
                {
                    score += 10;
                }

                var durationMatches = signature.Duration is { } expected &&
                                      candidate.Document.Duration is { } actual &&
                                      Math.Abs((expected - actual).TotalSeconds) <= 2d;

                if (durationMatches)
                {
                    score += 10;
                }

                return new LyricsSearchCandidate(
                    candidate,
                    score,
                    titleExact && artistExact && durationMatches);
            })
            .OrderByDescending(static item => item.MatchScore)
            .ThenByDescending(static item => item.IsExactSignatureMatch)
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
