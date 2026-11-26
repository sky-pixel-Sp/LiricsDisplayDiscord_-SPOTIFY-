using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class DiscordPresenceDataTests
{
    private static readonly DateTimeOffset Anchor =
        new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PlayingTrackPublishesTrackArtistAndAccurateCountdownTimestamps()
    {
        var track = CreateTrack(TimeSpan.FromSeconds(75), isPlaying: true, TimeSpan.FromSeconds(240));

        var result = DiscordPresenceData.FromTrack(track, Anchor.AddSeconds(5));

        Assert.AreEqual("Track", result.Details);
        Assert.AreEqual("Artist", result.State);
        Assert.IsTrue(result.IsPlaying);
        Assert.AreEqual(Anchor.AddSeconds(-70).ToUnixTimeSeconds(), result.StartTimestamp);
        Assert.AreEqual(Anchor.AddSeconds(170).ToUnixTimeSeconds(), result.EndTimestamp);
    }

    [TestMethod]
    public void PausedTrackDoesNotPublishAContinuingTimer()
    {
        var track = CreateTrack(TimeSpan.FromSeconds(75), isPlaying: false, TimeSpan.FromSeconds(240));

        var result = DiscordPresenceData.FromTrack(track, Anchor.AddMinutes(1));

        Assert.AreEqual("Artist · Paused", result.State);
        Assert.IsFalse(result.IsPlaying);
        Assert.IsNull(result.StartTimestamp);
        Assert.IsNull(result.EndTimestamp);
    }

    [TestMethod]
    public void PlayingTrackWithoutDurationPublishesElapsedTimeOnly()
    {
        var track = CreateTrack(TimeSpan.FromSeconds(12), isPlaying: true, duration: null);

        var result = DiscordPresenceData.FromTrack(track, Anchor);

        Assert.AreEqual(Anchor.AddSeconds(-12).ToUnixTimeSeconds(), result.StartTimestamp);
        Assert.IsNull(result.EndTimestamp);
    }

    [TestMethod]
    public void MetadataIsWhitespaceNormalizedAndLimitedToDiscordFieldLength()
    {
        var track = CreateTrack(TimeSpan.Zero, isPlaying: false, duration: null) with
        {
            Title = new string('T', 160),
            Artist = " Artist   Name "
        };

        var result = DiscordPresenceData.FromTrack(track, Anchor);

        Assert.AreEqual(128, result.Details.Length);
        Assert.AreEqual("Artist Name · Paused", result.State);
    }

    private static SpotifyTrackSnapshot CreateTrack(
        TimeSpan position,
        bool isPlaying,
        TimeSpan? duration) =>
        new(
            "Track",
            "Artist",
            null,
            position,
            duration,
            isPlaying,
            "Spotify.exe",
            Anchor);
}
