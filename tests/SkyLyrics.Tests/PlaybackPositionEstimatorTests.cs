using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class PlaybackPositionEstimatorTests
{
    private static readonly DateTimeOffset Anchor =
        new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AdvancesPositionWhilePlaying()
    {
        var snapshot = CreateSnapshot(position: TimeSpan.FromSeconds(10), isPlaying: true);

        var result = PlaybackPositionEstimator.Estimate(snapshot, Anchor.AddMilliseconds(1250));

        Assert.AreEqual(TimeSpan.FromMilliseconds(11250), result);
    }

    [TestMethod]
    public void DoesNotAdvancePositionWhilePaused()
    {
        var snapshot = CreateSnapshot(position: TimeSpan.FromSeconds(10), isPlaying: false);

        var result = PlaybackPositionEstimator.Estimate(snapshot, Anchor.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(10), result);
    }

    [TestMethod]
    public void ClampsEstimatedPositionToTrackDuration()
    {
        var snapshot = CreateSnapshot(
            position: TimeSpan.FromSeconds(59),
            isPlaying: true,
            duration: TimeSpan.FromSeconds(60));

        var result = PlaybackPositionEstimator.Estimate(snapshot, Anchor.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(60), result);
    }

    [TestMethod]
    public void DoesNotMoveBackwardWhenTimelineTimestampIsInTheFuture()
    {
        var snapshot = CreateSnapshot(position: TimeSpan.FromSeconds(10), isPlaying: true);

        var result = PlaybackPositionEstimator.Estimate(snapshot, Anchor.AddSeconds(-1));

        Assert.AreEqual(TimeSpan.FromSeconds(10), result);
    }

    private static SpotifyTrackSnapshot CreateSnapshot(
        TimeSpan position,
        bool isPlaying,
        TimeSpan? duration = null) =>
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
