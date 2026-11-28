using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LyricsMatchValidatorTests
{
    [Fact]
    public void AcceptsMatchingTitleArtistAndDurationWithinThreeSeconds()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive",
            "Example Artist",
            214.9);

        Assert.True(validation.IsMatch);
        Assert.True(validation.TitleMatches);
        Assert.True(validation.ArtistMatches);
        Assert.True(validation.DurationMatches);
    }

    [Fact]
    public void NormalizesFeaturingRemasterVersionsPunctuationAndAccents()
    {
        var expected = LyricsMatchValidator.NormalizeTitle(
            "  Café Racer (feat. Guest Artist) - Remastered 2024 (Versions)  ");
        var actual = LyricsMatchValidator.NormalizeTitle("Cafe Racer!");

        Assert.Equal("cafe racer", expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AcceptsRemasteredTitleWhenItsDurationStillMatches()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive - Remastered 2011",
            "Example Artist",
            213);

        Assert.True(validation.IsMatch);
    }

    [Fact]
    public void RejectsLiveRecordingWhenItsDurationDoesNotMatchTheStudioTrack()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive (Live)",
            "Example Artist",
            238);

        Assert.False(validation.IsMatch);
        Assert.True(validation.TitleMatches);
        Assert.False(validation.DurationMatches);
    }

    [Theory]
    [InlineData(215.0, true)]
    [InlineData(215.01, false)]
    [InlineData(208.99, false)]
    public void DurationToleranceIsThreeSeconds(double actualSeconds, bool expectedMatch)
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive",
            "Example Artist",
            actualSeconds);

        Assert.Equal(expectedMatch, validation.IsMatch);
        Assert.Equal(expectedMatch, validation.DurationMatches);
    }

    [Fact]
    public void AllowsArtistToMatchOneCreditInAFeaturingCollaboration()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive",
            "Guest Artist feat. Example Artist",
            212);

        Assert.True(validation.IsMatch);
        Assert.True(validation.ArtistMatchedPartially);
        Assert.Contains("cocok sebagian", validation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnUnrelatedArtistWhoseNameContainsTheExpectedArtist()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drive",
            "Example Artist Tribute Band",
            212);

        Assert.False(validation.IsMatch);
        Assert.False(validation.ArtistMatches);
    }

    [Fact]
    public void RejectsDifferentSongWithVerySimilarTitle()
    {
        var validation = Validate(
            "Night Drive",
            "Example Artist",
            212,
            "Night Drives",
            "Example Artist",
            212);

        Assert.False(validation.IsMatch);
        Assert.False(validation.TitleMatches);
    }

    [Fact]
    public void RejectsRecordWhenEitherDurationIsUnavailable()
    {
        var signature = new LyricsTrackSignature(
            "Night Drive",
            "Example Artist",
            null,
            null);
        var document = MakeDocument(
            "Night Drive",
            "Example Artist",
            212);

        var validation = LyricsMatchValidator.Validate(signature, document);

        Assert.False(validation.IsMatch);
        Assert.False(validation.DurationMatches);
        Assert.Contains("Durasi tidak tersedia", validation.Reason);
    }

    private static LyricsMatchValidationResult Validate(
        string expectedTitle,
        string expectedArtist,
        double expectedDuration,
        string actualTitle,
        string actualArtist,
        double actualDuration)
    {
        var signature = new LyricsTrackSignature(
            expectedTitle,
            expectedArtist,
            null,
            TimeSpan.FromSeconds(expectedDuration));

        return LyricsMatchValidator.Validate(
            signature,
            MakeDocument(actualTitle, actualArtist, actualDuration));
    }

    private static LyricsDocument MakeDocument(string title, string artist, double duration) =>
        new(
            title,
            artist,
            null,
            TimeSpan.FromSeconds(duration),
            false,
            [new LyricLine("Verified source line", TimeSpan.FromSeconds(1))]);
}
