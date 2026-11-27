using System.Collections.Immutable;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Core.Playback;

public sealed record TrackMetadata(
    string? Title,
    string? Artist,
    string? Album,
    string? AlbumArtist,
    ImmutableArray<byte> ArtworkBytes = default)
{
    public bool HasArtwork => !ArtworkBytes.IsDefaultOrEmpty;
}

public sealed record PlaybackSnapshot
{
    public PlaybackSnapshot(
        string sourceAppUserModelId,
        TrackMetadata? track,
        PlaybackStatus playbackStatus,
        TimeSpan position,
        TimeSpan startTime,
        TimeSpan? endTime,
        TimeSpan? minSeekTime,
        TimeSpan? maxSeekTime,
        double playbackRate,
        DateTimeOffset sourceTimelineUpdatedAtUtc,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAppUserModelId);

        if (position < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (startTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startTime));
        }

        if (endTime is not null && endTime < startTime)
        {
            throw new ArgumentOutOfRangeException(nameof(endTime));
        }

        if (!double.IsFinite(playbackRate) || playbackRate <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }

        SourceAppUserModelId = sourceAppUserModelId;
        Track = track;
        PlaybackStatus = playbackStatus;
        Position = position;
        StartTime = startTime;
        EndTime = endTime;
        MinSeekTime = minSeekTime;
        MaxSeekTime = maxSeekTime;
        PlaybackRate = playbackRate;
        SourceTimelineUpdatedAtUtc = sourceTimelineUpdatedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    public string SourceAppUserModelId { get; }
    public TrackMetadata? Track { get; }
    public PlaybackStatus PlaybackStatus { get; }
    public TimeSpan Position { get; }
    public TimeSpan StartTime { get; }
    public TimeSpan? EndTime { get; }
    public TimeSpan? MinSeekTime { get; }
    public TimeSpan? MaxSeekTime { get; }
    public double PlaybackRate { get; }
    public DateTimeOffset SourceTimelineUpdatedAtUtc { get; }
    public DateTimeOffset ReceivedAtUtc { get; }
}
