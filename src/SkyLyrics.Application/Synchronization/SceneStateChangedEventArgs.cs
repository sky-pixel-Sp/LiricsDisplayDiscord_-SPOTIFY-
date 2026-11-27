using SkyLyrics.Core.Scene;

namespace SkyLyrics.Application.Synchronization;

public sealed class SceneStateChangedEventArgs(SceneState state) : EventArgs
{
    public SceneState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
}
