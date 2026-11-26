using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class LyricsMatchValidatorTests
{
    [TestMethod]
    public void AcceptsMatchingTitleArtistAlbumAndDuration()
    {
        var result = LyricsMatchValidator.Evaluate(CreateTrack(), CreateLyrics());
        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void RejectsWrongTitle()
    {
        var result = LyricsMatchValidator.Evaluate(
            CreateTrack(),
            CreateLyrics(trackName: "Different song"));

        Assert.IsFalse(result.IsMatch);
        StringAssert.Contains(result.Reason, "title");
    }

    [TestMethod]
    public void RejectsWrongArtist()
    {
        var result = LyricsMatchValidator.Evaluate(
            CreateTrack(),
            CreateLyrics(artistName: "Different artist"));

        Assert.IsFalse(result.IsMatch);
        StringAssert.Contains(result.Reason, "Artist");
    }

    [TestMethod]
    public void RejectsDifferentAlbumWhenBothSidesProvideAlbum()
    {
        var result = LyricsMatchValidator.Evaluate(
            CreateTrack(),
            CreateLyrics(albumName: "Different album"));

        Assert.IsFalse(result.IsMatch);
        StringAssert.Contains(result.Reason, "Album");
    }

    [TestMethod]
    public void RejectsDurationOutsideTwoSecondTolerance()
    {
        var result = LyricsMatchValidator.Evaluate(
            CreateTrack(),
            CreateLyrics(duration: 183));

        Assert.IsFalse(result.IsMatch);
        StringAssert.Contains(result.Reason, "duration");
    }

    [TestMethod]
    public void AllowsPunctuationDifferencesWithoutChangingWords()
    {
        var track = CreateTrack() with { Title = "Song - (Remastered)" };
        var lyrics = CreateLyrics(trackName: "Song Remastered");

        var result = LyricsMatchValidator.Evaluate(track, lyrics);
        Assert.IsTrue(result.IsMatch);
    }

    private static SpotifyTrackSnapshot CreateTrack() =>
        new("Song", "Artist", "Album", TimeSpan.Zero, TimeSpan.FromSeconds(180), true, "Spotify");

    private static LrclibLyrics CreateLyrics(
        string trackName = "Song",
        string artistName = "Artist",
        string? albumName = "Album",
        double duration = 180) =>
        new(
            100,
            trackName,
            artistName,
            albumName,
            duration,
            false,
            "Example plain lyrics",
            null,
            false,
            null);
}
