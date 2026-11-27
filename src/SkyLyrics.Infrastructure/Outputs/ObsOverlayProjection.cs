using SkyLyrics.Core.Scene;

namespace SkyLyrics.Infrastructure.Outputs;

public sealed record ObsOverlayState(
    long Revision,
    string? Title,
    string? Artist,
    string? PreviousLine,
    string? CurrentLine,
    string? NextLine,
    string? ActiveWord,
    string PlaybackStatus,
    string Position);

public static class ObsOverlayProjection
{
    public static ObsOverlayState Map(SceneState scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var track = scene.Track;
        return new ObsOverlayState(
            scene.Revision,
            track?.Title,
            track?.Artist,
            scene.PreviousLine?.Text,
            scene.ActiveLine?.Text,
            scene.NextLine?.Text,
            scene.WordFill?.Word.Text,
            scene.PlaybackStatus.ToString(),
            $"{(long)scene.Position.TotalMinutes:00}:{scene.Position.Seconds:00}");
    }
}
