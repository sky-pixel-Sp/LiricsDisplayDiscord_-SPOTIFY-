using System.Text;
using SkyLyrics.Core.Scene;
using SkyLyrics.Infrastructure.Outputs;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class DiscordAndObsOutputTests
{
    [Fact]
    public async Task DiscordFrameCodec_RoundTripsHeaderAndPayload()
    {
        var expectedPayload = Encoding.UTF8.GetBytes("{\"v\":1,\"client_id\":\"12345678901234567\"}");
        await using var stream = new MemoryStream();

        await DiscordIpcFrameCodec.WriteAsync(stream, 1, expectedPayload);
        stream.Position = 0;
        var frame = await DiscordIpcFrameCodec.ReadAsync(stream);

        Assert.Equal(1, frame.Opcode);
        Assert.Equal(expectedPayload, frame.Payload);
    }

    [Fact]
    public void DiscordFrameCodec_RejectsPayloadAboveLimit()
    {
        var oversized = new byte[DiscordIpcFrameCodec.MaximumPayloadBytes + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => DiscordIpcFrameCodec.Encode(1, oversized));
    }

    [Fact]
    public void DiscordPresenceMapper_UsesActualTrackAndDoesNotInventElapsedTimeWhenPaused()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var scene = new SceneState(
            3,
            new TrackScene("Actual title", "Actual artist", "Actual album", null),
            null, null, null,
            TimeSpan.FromSeconds(75),
            PlaybackStatus.Paused,
            now);

        var activity = DiscordPresenceMapper.Map(scene, now);

        Assert.NotNull(activity);
        Assert.Equal("Actual title", activity.Details);
        Assert.Equal("Actual artist · Paused", activity.State);
        Assert.Equal(2, activity.Type);
        Assert.Null(activity.StartedAtUnixMilliseconds);
    }

    [Fact]
    public void DiscordPresenceMapper_ClearsForMissingTrackAndStoppedPlayback()
    {
        Assert.Null(DiscordPresenceMapper.Map(SceneState.Empty, DateTimeOffset.UtcNow));

        var stopped = new SceneState(
            4,
            new TrackScene("Actual title", "Actual artist", null, null),
            null, null, null,
            TimeSpan.Zero,
            PlaybackStatus.Stopped,
            DateTimeOffset.UtcNow);

        Assert.Null(DiscordPresenceMapper.Map(stopped, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ObsProjection_ContainsOnlySceneBackedLyricsAndMetadata()
    {
        var scene = new SceneState(
            8,
            new TrackScene("Song title", "Artist", null, null),
            new LyricLineScene(TimeSpan.FromSeconds(12), "Source lyric line"),
            new LyricLineScene(TimeSpan.FromSeconds(18), "Next source line"),
            null,
            TimeSpan.FromSeconds(13),
            PlaybackStatus.Playing,
            DateTimeOffset.UtcNow,
            new LyricLineScene(TimeSpan.FromSeconds(8), "Previous source line"));

        var projection = ObsOverlayProjection.Map(scene);

        Assert.Equal(8, projection.Revision);
        Assert.Equal("Song title", projection.Title);
        Assert.Equal("Source lyric line", projection.CurrentLine);
        Assert.Equal("Previous source line", projection.PreviousLine);
        Assert.Equal("Next source line", projection.NextLine);
    }
}
