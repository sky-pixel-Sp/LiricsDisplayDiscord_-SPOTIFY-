using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LyricsCandidateRankerTests
{
    [Fact]
    public void ExactTitleArtistAndDurationCandidateRanksFirst()
    {
        var signature = new LyricsTrackSignature(
            "Night Drive", "Example Artist", "Midnight Album", TimeSpan.FromSeconds(212));

        var candidates = new[]
        {
            Make("Night Drive (Live)", "Example Artist", "Live Album", 250, 10),
            Make("Night Drive", "Example Artist", "Midnight Album", 213, 11),
            Make("Other Song", "Other Artist", "Other Album", 212, 12)
        };

        var ranked = LyricsCandidateRanker.Rank(signature, candidates);

        Assert.Equal(11, ranked[0].Result.ProviderId);
        Assert.True(ranked[0].IsExactSignatureMatch);
        Assert.False(ranked[1].IsExactSignatureMatch);
    }

    [Fact]
    public void DurationOutsideThreeSecondsDoesNotCountAsExactSignatureMatch()
    {
        var signature = new LyricsTrackSignature(
            "Song", "Artist", "Album", TimeSpan.FromSeconds(200));

        var ranked = LyricsCandidateRanker.Rank(
            signature,
            [Make("Song", "Artist", "Album", 203, 1)]);

        Assert.False(ranked[0].IsExactSignatureMatch);
    }

    [Fact]
    public void NormalizesCaseAndRepeatedWhitespace()
    {
        var signature = new LyricsTrackSignature(
            "  Night   Drive ", "EXAMPLE ARTIST", "Midnight Album", TimeSpan.FromSeconds(100));

        var ranked = LyricsCandidateRanker.Rank(
            signature,
            [Make("night drive", "Example Artist", "Midnight Album", 101, 1)]);

        Assert.True(ranked[0].IsExactSignatureMatch);
    }

    [Fact]
    public void RankedCandidateIncludesValidationReasonAndScore()
    {
        var signature = new LyricsTrackSignature(
            "Song", "Artist", "Album", TimeSpan.FromSeconds(100));

        var ranked = LyricsCandidateRanker.Rank(
            signature,
            [Make("Song", "Artist", "Album", 101, 9)]);

        Assert.True(ranked[0].IsExactSignatureMatch);
        Assert.Equal(100, ranked[0].MatchScore);
        Assert.Contains("Judul cocok", ranked[0].MatchReason);
        Assert.Contains("Artis cocok", ranked[0].MatchReason);
    }

    [Fact]
    public void SimilarCandidateIsRankedButNeverMarkedExact()
    {
        var signature = new LyricsTrackSignature("Midnight", "Artist", null, TimeSpan.FromSeconds(100));

        var ranked = LyricsCandidateRanker.Rank(
            signature,
            [Make("Midnights", "Artist", null, 100, 2)]);

        Assert.True(ranked[0].MatchScore > 0);
        Assert.False(ranked[0].IsExactSignatureMatch);
    }

    private static LyricsProviderResult Make(
        string title,
        string artist,
        string? album,
        double durationSeconds,
        int providerId)
    {
        var document = new LyricsDocument(
            title,
            artist,
            album,
            TimeSpan.FromSeconds(durationSeconds),
            false,
            [new LyricLine("Sample line", TimeSpan.FromSeconds(1))]);

        return new LyricsProviderResult("Test provider", providerId, document);
    }
}
