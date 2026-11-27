using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Playback;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Application.Synchronization;

/// <summary>
/// Maintains a monotonic playback clock and publishes immutable lyric scenes.
/// It schedules one-shot timers for the next source timestamp; it does not poll.
/// </summary>
public sealed class SyncScheduler : IDisposable
{
    public static readonly TimeSpan MinimumOffset = TimeSpan.FromMilliseconds(-500);
    public static readonly TimeSpan MaximumOffset = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SyncScheduler> _logger;
    private readonly PlaybackClock _clock;
    private readonly ITimer _timer;

    private PlaybackSnapshot? _playback;
    private LyricsDocument? _lyrics;
    private TimeSpan _offset;
    private long _revision;
    private SceneState _current = SceneState.Empty;
    private bool _disposed;

    public SyncScheduler(
        TimeProvider? timeProvider = null,
        ILogger<SyncScheduler>? logger = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<SyncScheduler>.Instance;
        _clock = new PlaybackClock(_timeProvider);
        _timer = _timeProvider.CreateTimer(
            static state => ((SyncScheduler)state!).OnBoundaryTimer(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<SceneStateChangedEventArgs>? SceneChanged;

    public SceneState Current => Volatile.Read(ref _current);

    public TimeSpan Offset
    {
        get
        {
            lock (_gate)
            {
                return _offset;
            }
        }
    }

    public void UpdatePlayback(
        PlaybackSnapshot? snapshot,
        PlaybackSessionChangeKind changeKind)
    {
        SceneState? changed;
        lock (_gate)
        {
            ThrowIfDisposed();

            if (snapshot is null)
            {
                _playback = null;
                _lyrics = null;
                _clock.Reset();
                changed = RebuildLocked(force: true);
            }
            else
            {
                var trackChanged = _playback is not null &&
                    (!SameTrack(_playback, snapshot) ||
                     changeKind == PlaybackSessionChangeKind.TrackChanged);

                if (trackChanged)
                {
                    _lyrics = null;
                }

                _playback = snapshot;
                // GSMTC's snapshot position already includes the age of its source
                // timeline. Re-anchor at ReceivedAtUtc to avoid adding that age twice.
                _clock.Update(
                    snapshot.Position,
                    snapshot.ReceivedAtUtc,
                    snapshot.PlaybackRate,
                    snapshot.PlaybackStatus,
                    snapshot.StartTime,
                    snapshot.EndTime);

                changed = RebuildLocked(force: true);
            }
        }

        Publish(changed);
    }

    public void UpdateLyrics(LyricsDocument? document)
    {
        SceneState? changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            _lyrics = document;
            changed = RebuildLocked(force: true);
        }

        Publish(changed);
    }

    public void SetOffset(TimeSpan offset)
    {
        var bounded = offset < MinimumOffset
            ? MinimumOffset
            : offset > MaximumOffset
                ? MaximumOffset
                : offset;

        SceneState? changed;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_offset == bounded)
            {
                return;
            }

            _offset = bounded;
            changed = RebuildLocked(force: true);
        }

        Publish(changed);
    }

    private void OnBoundaryTimer()
    {
        SceneState? changed;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            changed = RebuildLocked(force: false);
        }

        Publish(changed);
    }

    private SceneState? RebuildLocked(bool force)
    {
        var position = GetEffectivePositionLocked();
        var status = _playback?.PlaybackStatus ?? PlaybackStatus.Unknown;
        var track = _playback?.Track is { } metadata
            ? new TrackScene(metadata.Title, metadata.Artist, metadata.Album, null, metadata.ArtworkBytes)
            : null;

        var groups = GetTimedGroupsLocked();
        var insertionIndex = UpperBound(groups, position);
        var activeGroup = insertionIndex > 0 ? groups[insertionIndex - 1] : null;
        var nextGroup = insertionIndex < groups.Length ? groups[insertionIndex] : null;

        var previousGroup = insertionIndex > 1 ? groups[insertionIndex - 2] : null;
        var previousLine = previousGroup is null ? null : ToScene(previousGroup);
        var activeLine = activeGroup is null ? null : ToScene(activeGroup);
        var nextLine = nextGroup is null ? null : ToScene(nextGroup);
        var wordFill = activeGroup is null
            ? null
            : GetActiveWordFill(activeGroup, position);

        if (!force && IsEquivalent(
                _current,
                track,
                previousLine,
                activeLine,
                nextLine,
                wordFill,
                position,
                status))
        {
            RescheduleLocked(groups, insertionIndex, activeGroup, position);
            return null;
        }

        var nextRevision = checked(++_revision);
        var scene = new SceneState(
            nextRevision,
            track,
            activeLine,
            nextLine,
            wordFill,
            position,
            status,
            _timeProvider.GetUtcNow(),
            previousLine);

        Volatile.Write(ref _current, scene);
        RescheduleLocked(groups, insertionIndex, activeGroup, position);
        return scene;
    }

    private TimeSpan GetEffectivePositionLocked()
    {
        if (_playback is null)
        {
            return TimeSpan.Zero;
        }

        var position = SaturatingAdd(_clock.Position, _offset);
        if (position < _playback.StartTime)
        {
            position = _playback.StartTime;
        }

        if (_playback.EndTime is { } end && position > end)
        {
            position = end;
        }

        return position < TimeSpan.Zero ? TimeSpan.Zero : position;
    }

    private ImmutableArray<TimedLineGroup> GetTimedGroupsLocked()
    {
        if (_lyrics is null || _lyrics.SyncMode == LyricsSyncMode.Unsynced ||
            _lyrics.SyncMode == LyricsSyncMode.Instrumental)
        {
            return ImmutableArray<TimedLineGroup>.Empty;
        }

        return _lyrics.Lines
            .Select((line, index) => (Line: line, Index: index))
            .Where(static item => item.Line.Start is not null)
            .OrderBy(static item => item.Line.Start)
            .ThenBy(static item => item.Index)
            .GroupBy(static item => item.Line.Start!.Value)
            .Select(group =>
            {
                var items = group.ToArray();
                var text = string.Join(
                    Environment.NewLine,
                    items.Select(static item => item.Line.Text));

                var words = items
                    .SelectMany(static item => item.Line.Words)
                    .Where(static word => word.End is null || word.End > word.Start)
                    .Select(static word => new LyricWordScene(word.Start, word.End, word.Text))
                    .OrderBy(static word => word.Start)
                    .ToImmutableArray();

                return new TimedLineGroup(group.Key, text, words);
            })
            .ToImmutableArray();
    }

    private static int UpperBound(
        ImmutableArray<TimedLineGroup> groups,
        TimeSpan position)
    {
        var low = 0;
        var high = groups.Length;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (groups[middle].Start <= position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static LyricLineScene ToScene(TimedLineGroup group) =>
        new(group.Start, group.Text, group.Words);

    private static WordFillState? GetActiveWordFill(
        TimedLineGroup group,
        TimeSpan position)
    {
        LyricWordScene? activeWord = null;
        foreach (var word in group.Words)
        {
            if (word.Start > position)
            {
                break;
            }

            activeWord = word;
        }

        if (activeWord is null)
        {
            return null;
        }

        if (activeWord.End is not { } end)
        {
            // Without a source end timestamp, identify the active word but do not
            // fabricate a duration or progress percentage.
            return new WordFillState(activeWord, 0d);
        }

        if (position >= end || end <= activeWord.Start)
        {
            return null;
        }

        var progress = (double)(position.Ticks - activeWord.Start.Ticks) /
                       (end.Ticks - activeWord.Start.Ticks);
        return new WordFillState(activeWord, Math.Clamp(progress, 0d, 1d));
    }

    private void RescheduleLocked(
        ImmutableArray<TimedLineGroup> groups,
        int insertionIndex,
        TimedLineGroup? activeGroup,
        TimeSpan position)
    {
        if (_playback?.PlaybackStatus != PlaybackStatus.Playing ||
            _lyrics is null ||
            groups.IsEmpty)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        TimeSpan? nextBoundary = insertionIndex < groups.Length
            ? groups[insertionIndex].Start
            : null;

        if (activeGroup is not null && !activeGroup.Words.IsEmpty)
        {
            foreach (var word in activeGroup.Words)
            {
                if (word.Start > position &&
                    (nextBoundary is null || word.Start < nextBoundary))
                {
                    nextBoundary = word.Start;
                }

                if (word.End is { } wordEnd && wordEnd > position &&
                    (nextBoundary is null || wordEnd < nextBoundary))
                {
                    nextBoundary = wordEnd;
                }
            }
        }

        if (nextBoundary is null)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        var mediaDelta = nextBoundary.Value - position;
        if (mediaDelta <= TimeSpan.Zero)
        {
            _timer.Change(TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan);
            return;
        }

        var rate = _clock.PlaybackRate;
        var wallTicks = mediaDelta.Ticks / rate;
        var due = wallTicks >= TimeSpan.MaxValue.Ticks
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(wallTicks)));

        // Windows timers have finite practical resolution. A one-shot 1 ms floor
        // avoids zero-delay re-entry while preserving event-driven scheduling.
        if (due < TimeSpan.FromMilliseconds(1))
        {
            due = TimeSpan.FromMilliseconds(1);
        }

        _timer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private static bool IsEquivalent(
        SceneState current,
        TrackScene? track,
        LyricLineScene? previousLine,
        LyricLineScene? activeLine,
        LyricLineScene? nextLine,
        WordFillState? wordFill,
        TimeSpan position,
        PlaybackStatus status) =>
        Equals(current.Track, track) &&
        Equals(current.PreviousLine, previousLine) &&
        Equals(current.ActiveLine, activeLine) &&
        Equals(current.NextLine, nextLine) &&
        Equals(current.WordFill, wordFill) &&
        current.Position == position &&
        current.PlaybackStatus == status;

    private static bool SameTrack(PlaybackSnapshot left, PlaybackSnapshot right) =>
        StringComparer.OrdinalIgnoreCase.Equals(
            left.SourceAppUserModelId,
            right.SourceAppUserModelId) &&
        left.Track is null
            ? right.Track is null
            : right.Track is not null &&
              StringComparer.OrdinalIgnoreCase.Equals(left.Track.Title, right.Track.Title) &&
              StringComparer.OrdinalIgnoreCase.Equals(left.Track.Artist, right.Track.Artist) &&
              StringComparer.OrdinalIgnoreCase.Equals(left.Track.Album, right.Track.Album);

    private static TimeSpan SaturatingAdd(TimeSpan left, TimeSpan right)
    {
        if (right.Ticks > 0 && left.Ticks > long.MaxValue - right.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        if (right.Ticks < 0 && left.Ticks < long.MinValue - right.Ticks)
        {
            return TimeSpan.MinValue;
        }

        return TimeSpan.FromTicks(left.Ticks + right.Ticks);
    }

    private void Publish(SceneState? state)
    {
        if (state is null)
        {
            return;
        }

        var handlers = SceneChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new SceneStateChangedEventArgs(state);
        foreach (EventHandler<SceneStateChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scene state subscriber failed at revision {SceneRevision}.",
                    state.Revision);
            }
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Dispose();
        }
    }

    private sealed record TimedLineGroup(
        TimeSpan Start,
        string Text,
        ImmutableArray<LyricWordScene> Words);
}
