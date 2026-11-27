using SkyLyrics.Core.Scene;

namespace SkyLyrics.Core.Playback;

public sealed class PlaybackClock
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    private long _anchorTimestamp;
    private TimeSpan _anchorPosition;
    private TimeSpan _startTime;
    private TimeSpan? _endTime;
    private double _playbackRate = 1d;
    private PlaybackStatus _playbackStatus = PlaybackStatus.Unknown;
    private bool _hasAnchor;

    public PlaybackClock(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _anchorTimestamp = _timeProvider.GetTimestamp();
    }

    public PlaybackStatus PlaybackStatus
    {
        get
        {
            lock (_sync)
            {
                return _playbackStatus;
            }
        }
    }

    public double PlaybackRate
    {
        get
        {
            lock (_sync)
            {
                return _playbackRate;
            }
        }
    }

    public TimeSpan Position => GetPosition();

    public void Update(
        TimeSpan sourcePosition,
        DateTimeOffset sourceUpdatedAtUtc,
        double playbackRate,
        PlaybackStatus playbackStatus,
        TimeSpan startTime,
        TimeSpan? endTime)
    {
        if (sourcePosition < TimeSpan.Zero)
        {
            sourcePosition = TimeSpan.Zero;
        }

        var normalizedRate = NormalizeRate(playbackRate);
        var normalizedStart = startTime < TimeSpan.Zero ? TimeSpan.Zero : startTime;
        var normalizedEnd = endTime is not null && endTime > normalizedStart ? endTime : null;

        lock (_sync)
        {
            var age = _timeProvider.GetUtcNow() - sourceUpdatedAtUtc;
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            var adjustedPosition = sourcePosition;
            if (playbackStatus == PlaybackStatus.Playing && age > TimeSpan.Zero)
            {
                adjustedPosition = SaturatingAdd(adjustedPosition, Scale(age, normalizedRate));
            }

            _startTime = normalizedStart;
            _endTime = normalizedEnd;
            _playbackRate = normalizedRate;
            _playbackStatus = playbackStatus;
            _anchorPosition = Clamp(adjustedPosition);
            _anchorTimestamp = _timeProvider.GetTimestamp();
            _hasAnchor = true;
        }
    }

    public void ChangePlaybackStatus(PlaybackStatus playbackStatus)
    {
        lock (_sync)
        {
            var currentPosition = GetPositionLocked();
            _anchorPosition = currentPosition;
            _anchorTimestamp = _timeProvider.GetTimestamp();
            _playbackStatus = playbackStatus;
            _hasAnchor = true;
        }
    }

    public void ChangePlaybackRate(double playbackRate)
    {
        var normalizedRate = NormalizeRate(playbackRate);

        lock (_sync)
        {
            var currentPosition = GetPositionLocked();
            _anchorPosition = currentPosition;
            _anchorTimestamp = _timeProvider.GetTimestamp();
            _playbackRate = normalizedRate;
            _hasAnchor = true;
        }
    }

    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        lock (_sync)
        {
            _anchorPosition = Clamp(position);
            _anchorTimestamp = _timeProvider.GetTimestamp();
            _hasAnchor = true;
        }
    }

    public TimeSpan GetPosition()
    {
        lock (_sync)
        {
            return GetPositionLocked();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _anchorPosition = TimeSpan.Zero;
            _anchorTimestamp = _timeProvider.GetTimestamp();
            _startTime = TimeSpan.Zero;
            _endTime = null;
            _playbackRate = 1d;
            _playbackStatus = PlaybackStatus.Unknown;
            _hasAnchor = false;
        }
    }

    private TimeSpan GetPositionLocked()
    {
        if (!_hasAnchor)
        {
            return TimeSpan.Zero;
        }

        var position = _anchorPosition;
        if (_playbackStatus == PlaybackStatus.Playing)
        {
            var elapsed = _timeProvider.GetElapsedTime(
                _anchorTimestamp,
                _timeProvider.GetTimestamp());

            if (elapsed > TimeSpan.Zero)
            {
                position = SaturatingAdd(position, Scale(elapsed, _playbackRate));
            }
        }

        return Clamp(position);
    }

    private TimeSpan Clamp(TimeSpan position)
    {
        if (position < _startTime)
        {
            return _startTime;
        }

        if (_endTime is not null && position > _endTime.Value)
        {
            return _endTime.Value;
        }

        return position;
    }

    private static double NormalizeRate(double playbackRate) =>
        double.IsFinite(playbackRate) && playbackRate > 0d ? playbackRate : 1d;

    private static TimeSpan Scale(TimeSpan duration, double rate)
    {
        var ticks = duration.Ticks * rate;
        if (ticks >= long.MaxValue)
        {
            return TimeSpan.MaxValue;
        }

        if (ticks <= long.MinValue)
        {
            return TimeSpan.MinValue;
        }

        return TimeSpan.FromTicks((long)Math.Round(ticks, MidpointRounding.AwayFromZero));
    }

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
}
