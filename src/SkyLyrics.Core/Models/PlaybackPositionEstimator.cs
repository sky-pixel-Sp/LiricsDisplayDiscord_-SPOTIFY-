namespace SkyLyrics.Core.Models;

/// <summary>
/// Estimates the current media position from the last authoritative Windows
/// timeline sample. This only advances the clock; it never changes lyric timestamps.
/// </summary>
public static class PlaybackPositionEstimator
{
    public static TimeSpan Estimate(SpotifyTrackSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.IsPlaying)
            return snapshot.Position;

        var anchor = snapshot.PositionUpdatedAtUtc == default
            ? now
            : snapshot.PositionUpdatedAtUtc;
        var elapsed = now - anchor;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var estimated = snapshot.Position + elapsed;
        return snapshot.Duration is { } duration &&
               duration > TimeSpan.Zero &&
               estimated > duration
            ? duration
            : estimated;
    }
}
