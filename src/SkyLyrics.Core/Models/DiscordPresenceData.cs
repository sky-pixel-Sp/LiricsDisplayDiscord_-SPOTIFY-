namespace SkyLyrics.Core.Models;

/// <summary>
/// Source-derived information that SkyLyrics is allowed to publish as Discord Rich Presence.
/// No lyric text, playback controls, or fabricated track metadata are included.
/// </summary>
public sealed record DiscordPresenceData(
    string Details,
    string State,
    long? StartTimestamp,
    long? EndTimestamp,
    bool IsPlaying)
{
    public static DiscordPresenceData FromTrack(SpotifyTrackSnapshot track, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(track);

        var details = NormalizeAndLimit(track.Title, 128);
        if (details.Length == 0)
            details = "Unknown track";

        var artist = NormalizeAndLimit(track.Artist, 128);
        var state = track.IsPlaying
            ? (artist.Length == 0 ? "Spotify Desktop" : artist)
            : (artist.Length == 0 ? "Paused" : $"{artist} · Paused");

        state = NormalizeAndLimit(state, 128);

        if (!track.IsPlaying)
            return new DiscordPresenceData(details, state, null, null, false);

        var position = PlaybackPositionEstimator.Estimate(track, now);
        var start = now.ToUnixTimeSeconds() - Math.Max(0L, (long)Math.Floor(position.TotalSeconds));
        long? end = track.Duration is { } duration && duration > TimeSpan.Zero
            ? start + (long)Math.Ceiling(duration.TotalSeconds)
            : null;

        return new DiscordPresenceData(details, state, start, end, true);
    }

    private static string NormalizeAndLimit(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length <= maximumLength)
            return normalized;

        var length = maximumLength;
        if (length > 0 && char.IsHighSurrogate(normalized[length - 1]))
            length--;

        return normalized[..length];
    }
}
