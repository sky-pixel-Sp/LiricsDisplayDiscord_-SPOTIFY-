using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class PlaybackClockTests
{
    [Fact]
    public void PlayingPositionAdvancesUsingMonotonicElapsedTimeAndPlaybackRate()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(10),
            time.GetUtcNow(),
            1.5,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(100));

        time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(TimeSpan.FromSeconds(10.3), clock.Position);
    }

    [Fact]
    public void TimelineAgeIsAppliedOnceAtTheAnchor()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);
        var sourceUpdatedAt = time.GetUtcNow().AddMilliseconds(-250);

        clock.Update(
            TimeSpan.FromSeconds(3),
            sourceUpdatedAt,
            2d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(50));

        Assert.Equal(TimeSpan.FromSeconds(3.5), clock.Position);
    }

    [Fact]
    public void PausedPlaybackDoesNotAdvance()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(8),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Paused,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(40));

        time.Advance(TimeSpan.FromSeconds(12));

        Assert.Equal(TimeSpan.FromSeconds(8), clock.Position);
    }

    [Fact]
    public void StoppedPlaybackDoesNotAdvance()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(4),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Stopped,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(40));

        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(4), clock.Position);
    }

    [Fact]
    public void PauseTransitionCapturesTheInterpolatedPositionAndFreezesIt()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(2),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(40));

        time.Advance(TimeSpan.FromMilliseconds(700));
        clock.ChangePlaybackStatus(PlaybackStatus.Paused);
        time.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.FromSeconds(2.7), clock.Position);
    }

    [Fact]
    public void ResumeContinuesFromTheFrozenPosition()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(2),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Paused,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(40));

        time.Advance(TimeSpan.FromSeconds(2));
        clock.ChangePlaybackStatus(PlaybackStatus.Playing);
        time.Advance(TimeSpan.FromMilliseconds(800));

        Assert.Equal(TimeSpan.FromSeconds(2.8), clock.Position);
    }

    [Fact]
    public void SeekImmediatelyChangesTheClockPosition()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(1),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(40));

        clock.Seek(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromSeconds(15), clock.Position);
    }

    [Fact]
    public void PositionIsClampedToTheTimelineEnd()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(9),
            time.GetUtcNow(),
            2d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10));

        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(10), clock.Position);
    }

    [Fact]
    public void PositionIsClampedToTheTimelineStart()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.Zero,
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Paused,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(5), clock.Position);
    }

    [Fact]
    public void FutureSourceTimestampDoesNotMovePositionBackwards()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(6),
            time.GetUtcNow().AddSeconds(1),
            1d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(6), clock.Position);
    }

    [Fact]
    public void InvalidPlaybackRateFallsBackToNormalSpeed()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(1),
            time.GetUtcNow(),
            double.NaN,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(20));

        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(1d, clock.PlaybackRate);
        Assert.Equal(TimeSpan.FromSeconds(3), clock.Position);
    }

    [Fact]
    public void ResetClearsPlaybackStateAndPosition()
    {
        var time = new ManualTimeProvider();
        var clock = new PlaybackClock(time);

        clock.Update(
            TimeSpan.FromSeconds(20),
            time.GetUtcNow(),
            1d,
            PlaybackStatus.Playing,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30));

        clock.Reset();
        time.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.Zero, clock.Position);
        Assert.Equal(PlaybackStatus.Unknown, clock.PlaybackStatus);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 11, 27, 0, 0, 0, TimeSpan.Zero);
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed)
        {
            _timestamp = checked(_timestamp + elapsed.Ticks);
            _utcNow = _utcNow.Add(elapsed);
        }
    }
}
