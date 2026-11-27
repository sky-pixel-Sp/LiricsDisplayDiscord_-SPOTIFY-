using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class Phase4ArtworkAndSceneTests
{
    [Fact]
    public void ArtworkBytesFlowFromPlaybackMetadataIntoPublishedScene()
    {
        var artwork = ImmutableArray.CreateRange(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var now = DateTimeOffset.UtcNow;
        var metadata = new TrackMetadata("Track", "Artist", "Album", "Artist", artwork);
        var snapshot = new PlaybackSnapshot(
            "Spotify.exe",
            metadata,
            PlaybackStatus.Paused,
            TimeSpan.FromSeconds(4),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            1d,
            now,
            now);

        using var scheduler = new SyncScheduler(TimeProvider.System, NullLogger<SyncScheduler>.Instance);
        scheduler.UpdatePlayback(snapshot, PlaybackSessionChangeKind.Connected);

        Assert.True(metadata.HasArtwork);
        Assert.True(scheduler.Current.Track!.HasArtwork);
        Assert.Equal(artwork, scheduler.Current.Track.ArtworkBytes);
    }

    [Fact]
    public void TrackWithoutGsmTcArtworkIsExplicitlyUnavailable()
    {
        var metadata = new TrackMetadata("Track", "Artist", "Album", "Artist");

        Assert.False(metadata.HasArtwork);
        Assert.True(metadata.ArtworkBytes.IsDefault);
    }

    [Fact]
    public void SceneProvidesPreviousActiveAndUpcomingLineContext()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new PlaybackSnapshot(
            "Spotify.exe",
            new TrackMetadata("Track", "Artist", "Album", "Artist"),
            PlaybackStatus.Paused,
            TimeSpan.FromMilliseconds(2500),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            1d,
            now,
            now);

        using var scheduler = new SyncScheduler(TimeProvider.System, NullLogger<SyncScheduler>.Instance);
        scheduler.UpdatePlayback(snapshot, PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(new SkyLyrics.Core.Lyrics.LyricsDocument(
            "Track",
            "Artist",
            "Album",
            TimeSpan.FromSeconds(60),
            instrumental: false,
            [
                new SkyLyrics.Core.Lyrics.LyricLine("Previous", TimeSpan.FromSeconds(1)),
                new SkyLyrics.Core.Lyrics.LyricLine("Active", TimeSpan.FromSeconds(2)),
                new SkyLyrics.Core.Lyrics.LyricLine("Upcoming", TimeSpan.FromSeconds(3))
            ]));

        Assert.Equal("Previous", scheduler.Current.PreviousLine?.Text);
        Assert.Equal("Active", scheduler.Current.ActiveLine?.Text);
        Assert.Equal("Upcoming", scheduler.Current.NextLine?.Text);
    }
}
