namespace SkyLyrics.Core.Scene;

public readonly record struct LyricLineIdentity(
    string TrackTitle,
    string TrackArtist,
    int LineIndex)
{
    public static LyricLineIdentity? From(SceneState scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (scene.Track is not { } track || scene.ActiveLine is not { } line)
        {
            return null;
        }

        return new LyricLineIdentity(
            track.Title?.Trim() ?? string.Empty,
            track.Artist?.Trim() ?? string.Empty,
            line.LineIndex);
    }
}
