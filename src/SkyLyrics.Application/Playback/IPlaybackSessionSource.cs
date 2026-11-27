using SkyLyrics.Core.Playback;

namespace SkyLyrics.Application.Playback;

public enum PlaybackSessionChangeKind
{
    Connected = 0,
    Disconnected = 1,
    TrackChanged = 2,
    PlaybackChanged = 3,
    TimelineChanged = 4
}

public sealed class PlaybackSessionChangedEventArgs(
    PlaybackSnapshot? snapshot,
    PlaybackSessionChangeKind changeKind) : EventArgs
{
    public PlaybackSnapshot? Snapshot { get; } = snapshot;

    public PlaybackSessionChangeKind ChangeKind { get; } = changeKind;
}

public interface IPlaybackSessionSource
{
    PlaybackSnapshot? Current { get; }

    event EventHandler<PlaybackSessionChangedEventArgs>? SnapshotChanged;
}
