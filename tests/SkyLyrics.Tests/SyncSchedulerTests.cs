using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class SyncSchedulerTests
{
    [Fact]
    public void SelectsMostRecentLineAndUpcomingLineAtCurrentPosition()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(12)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First", TimeSpan.FromSeconds(2)),
            new LyricLine("Second", TimeSpan.FromSeconds(8)),
            new LyricLine("Third", TimeSpan.FromSeconds(15))));

        Assert.Equal("Second", scheduler.Current.ActiveLine?.Text);
        Assert.Equal("Third", scheduler.Current.NextLine?.Text);
    }

    [Fact]
    public void DoesNotActivateLineBeforeItsSourceTimestamp()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("First", TimeSpan.FromSeconds(2))));

        Assert.Null(scheduler.Current.ActiveLine);
        Assert.Equal("First", scheduler.Current.NextLine?.Text);
    }

    [Fact]
    public void OneShotTimerPublishesTheNextLineWithoutPolling()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        var revisions = new List<long>();
        scheduler.SceneChanged += (_, args) => revisions.Add(args.State.Revision);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.Zero), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First", TimeSpan.FromSeconds(2)),
            new LyricLine("Second", TimeSpan.FromSeconds(5))));

        var before = scheduler.Current.Revision;
        time.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Null(scheduler.Current.ActiveLine);
        Assert.Equal(before, scheduler.Current.Revision);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("First", scheduler.Current.ActiveLine?.Text);
        Assert.True(scheduler.Current.Revision > before);
        Assert.Contains(scheduler.Current.Revision, revisions);
    }

    [Fact]
    public void RepeatedLyricTextRetainsDistinctSourceLineIndexes()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("Again", TimeSpan.FromSeconds(1)),
            new LyricLine("Again", TimeSpan.FromSeconds(2))));

        Assert.Equal("Again", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(0, scheduler.Current.ActiveLineIndex);

        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(2)), PlaybackSessionChangeKind.TimelineChanged);

        Assert.Equal("Again", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(1, scheduler.Current.ActiveLineIndex);
    }

    [Fact]
    public void IncomingLineEntersNinetyMillisecondsBeforeItsSourceTimestamp()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.Zero), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First", TimeSpan.FromSeconds(1)),
            new LyricLine("Second", TimeSpan.FromSeconds(2))));

        time.Advance(TimeSpan.FromMilliseconds(1909));
        Assert.Equal("First", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(0, scheduler.Current.ActiveLineIndex);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("Second", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(1, scheduler.Current.ActiveLineIndex);

        time.Advance(TimeSpan.FromMilliseconds(89));
        Assert.Equal(1, scheduler.Current.ActiveLineIndex);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, scheduler.Current.ActiveLineIndex);
    }

    [Fact]
    public void PauseSuppressesEarlyLineTransitionAndResumeReevaluatesIt()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1.95), PlaybackStatus.Paused), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First", TimeSpan.FromSeconds(1)),
            new LyricLine("Second", TimeSpan.FromSeconds(2))));

        Assert.Equal("First", scheduler.Current.ActiveLine?.Text);

        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1.95), PlaybackStatus.Playing), PlaybackSessionChangeKind.PlaybackChanged);

        Assert.Equal("Second", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(1, scheduler.Current.ActiveLineIndex);
    }

    [Fact]
    public void ZeroLeadTimeKeepsLyricsOnTheirExactSourceBoundary()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.SetLineAnimationLeadTime(TimeSpan.Zero);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1.95)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First", TimeSpan.FromSeconds(1)),
            new LyricLine("Second", TimeSpan.FromSeconds(2))));

        Assert.Equal("First", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(0, scheduler.Current.ActiveLineIndex);
    }

    [Fact]
    public void PauseFreezesPositionAndDisablesBoundaryAdvancement()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.Zero, PlaybackStatus.Playing), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("First", TimeSpan.FromSeconds(3))));

        time.Advance(TimeSpan.FromSeconds(1));
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1), PlaybackStatus.Paused), PlaybackSessionChangeKind.PlaybackChanged);
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.Current.Position);
        Assert.Null(scheduler.Current.ActiveLine);
    }

    [Fact]
    public void SeekRecomputesActiveLineImmediately()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("One", TimeSpan.FromSeconds(1)),
            new LyricLine("Two", TimeSpan.FromSeconds(10)),
            new LyricLine("Three", TimeSpan.FromSeconds(20))));

        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(22)), PlaybackSessionChangeKind.TimelineChanged);

        Assert.Equal("Three", scheduler.Current.ActiveLine?.Text);
        Assert.Null(scheduler.Current.NextLine);
    }

    [Fact]
    public void OffsetIsBoundedToTheProductSpecification()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);

        scheduler.SetOffset(TimeSpan.FromSeconds(4));
        Assert.Equal(TimeSpan.FromMilliseconds(500), scheduler.Offset);

        scheduler.SetOffset(TimeSpan.FromSeconds(-4));
        Assert.Equal(TimeSpan.FromMilliseconds(-500), scheduler.Offset);
    }

    [Fact]
    public void PositiveOffsetCanActivateLineEarlierWithoutChangingSourceTimestamp()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(1.8)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("Line", TimeSpan.FromSeconds(2))));

        scheduler.SetOffset(TimeSpan.FromMilliseconds(200));

        Assert.Equal("Line", scheduler.Current.ActiveLine?.Text);
        Assert.Equal(TimeSpan.FromSeconds(2), scheduler.Current.ActiveLine?.Start);
        Assert.Equal(TimeSpan.FromSeconds(2), scheduler.Current.Position);
    }

    [Fact]
    public void NegativeOffsetIsClampedAtTimelineStart()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(
            time,
            TimeSpan.FromSeconds(5),
            start: TimeSpan.FromSeconds(5)), PlaybackSessionChangeKind.Connected);
        scheduler.SetOffset(TimeSpan.FromMilliseconds(-500));

        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Current.Position);
    }

    [Fact]
    public void WordProgressUsesOnlySourceSuppliedStartAndEndTimes()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromMilliseconds(1500)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine(
            "Hello world",
            TimeSpan.FromSeconds(1),
            words:
            [
                new LyricWord(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Hello"),
                new LyricWord(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "world")
            ])));

        Assert.Equal("Hello", scheduler.Current.WordFill?.Word.Text);
        Assert.Equal(0.5d, scheduler.Current.WordFill!.Progress, precision: 3);
    }

    [Fact]
    public void WordWithoutSourceEndHasNoInventedProgressDuration()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromMilliseconds(1750)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine(
            "Hello world",
            TimeSpan.FromSeconds(1),
            words:
            [
                new LyricWord(TimeSpan.FromSeconds(1), null, "Hello"),
                new LyricWord(TimeSpan.FromSeconds(2), null, "world")
            ])));

        Assert.Equal("Hello", scheduler.Current.WordFill?.Word.Text);
        Assert.Equal(0d, scheduler.Current.WordFill?.Progress);
    }

    [Fact]
    public void UnsyncedLyricsNeverReceiveSyntheticActiveLine()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(20)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(LyricsDocument.FromPlainText(
            "Title", "Artist", null, null, "Actual plain lyrics\nSecond line"));

        Assert.Null(scheduler.Current.ActiveLine);
        Assert.Null(scheduler.Current.NextLine);
    }

    [Fact]
    public void SameTimestampLinesAreGroupedWithoutDroppingText()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(4)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(
            new LyricLine("First voice", TimeSpan.FromSeconds(3)),
            new LyricLine("Second voice", TimeSpan.FromSeconds(3)),
            new LyricLine("Next", TimeSpan.FromSeconds(6))));

        Assert.Equal($"First voice{Environment.NewLine}Second voice", scheduler.Current.ActiveLine?.Text);
        Assert.Equal("Next", scheduler.Current.NextLine?.Text);
    }

    [Fact]
    public void DisconnectClearsTrackAndLyricsImmediately()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(4)), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("Line", TimeSpan.FromSeconds(1))));

        scheduler.UpdatePlayback(null, PlaybackSessionChangeKind.Disconnected);

        Assert.Null(scheduler.Current.Track);
        Assert.Null(scheduler.Current.ActiveLine);
        Assert.Equal(PlaybackStatus.Unknown, scheduler.Current.PlaybackStatus);
        Assert.Equal(TimeSpan.Zero, scheduler.Current.Position);
    }

    [Fact]
    public void TrackChangeClearsPreviousTrackLyricsBeforeNewDocumentArrives()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(4), title: "First"), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("Old lyric", TimeSpan.FromSeconds(1))));

        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.FromSeconds(4), title: "Second"), PlaybackSessionChangeKind.TrackChanged);

        Assert.Equal("Second", scheduler.Current.Track?.Title);
        Assert.Null(scheduler.Current.ActiveLine);
        Assert.Null(scheduler.Current.NextLine);
    }

    [Fact]
    public void PlaybackRateChangesWallClockDelayToNextBoundary()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.Zero, rate: 2d), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("Line", TimeSpan.FromSeconds(4))));

        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal("Line", scheduler.Current.ActiveLine?.Text);
    }

    [Fact]
    public void SceneStateIsPublishedAsImmutableRevisionedSnapshots()
    {
        var time = new ManualTimeProvider();
        using var scheduler = new SyncScheduler(time);
        var published = new List<SceneState>();
        scheduler.SceneChanged += (_, args) => published.Add(args.State);

        scheduler.UpdatePlayback(Snapshot(time, TimeSpan.Zero), PlaybackSessionChangeKind.Connected);
        scheduler.UpdateLyrics(Document(new LyricLine("Line", TimeSpan.FromSeconds(1))));

        Assert.NotEmpty(published);
        Assert.True(published.Zip(published.Skip(1), (left, right) => right.Revision > left.Revision).All(static value => value));
    }

    private static PlaybackSnapshot Snapshot(
        ManualTimeProvider time,
        TimeSpan position,
        PlaybackStatus status = PlaybackStatus.Playing,
        string title = "Track",
        double rate = 1d,
        TimeSpan? start = null) =>
        new(
            "Spotify.exe",
            new TrackMetadata(title, "Artist", "Album", "Artist"),
            status,
            position,
            start ?? TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(60),
            rate,
            time.GetUtcNow(),
            time.GetUtcNow());

    private static LyricsDocument Document(params LyricLine[] lines) =>
        new("Track", "Artist", "Album", TimeSpan.FromSeconds(60), false, lines);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _utcNow = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override long GetTimestamp()
        {
            lock (_gate)
            {
                return _timestamp;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            }

            long target;
            lock (_gate)
            {
                _timestamp = checked(_timestamp + elapsed.Ticks);
                _utcNow = _utcNow.Add(elapsed);
                target = _timestamp;
            }

            while (true)
            {
                ManualTimer? due;
                lock (_gate)
                {
                    due = _timers
                        .Where(timer => !timer.Disposed && timer.DueTimestamp <= target)
                        .OrderBy(static timer => timer.DueTimestamp)
                        .FirstOrDefault();

                    if (due is null)
                    {
                        return;
                    }

                    due.MarkFired();
                }

                due.Invoke();
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private long _dueTimestamp = long.MaxValue;
            private TimeSpan _period = Timeout.InfiniteTimeSpan;

            public bool Disposed { get; private set; }
            public long DueTimestamp => _dueTimestamp;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner._gate)
                {
                    if (Disposed)
                    {
                        return false;
                    }

                    _period = period;
                    _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                        ? long.MaxValue
                        : checked(owner._timestamp + Math.Max(0, dueTime.Ticks));
                    return true;
                }
            }

            public void MarkFired()
            {
                if (_period == Timeout.InfiniteTimeSpan)
                {
                    _dueTimestamp = long.MaxValue;
                }
                else
                {
                    _dueTimestamp = checked(_dueTimestamp + _period.Ticks);
                }
            }

            public void Invoke() => callback(state);

            public void Dispose()
            {
                lock (owner._gate)
                {
                    Disposed = true;
                    _dueTimestamp = long.MaxValue;
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
