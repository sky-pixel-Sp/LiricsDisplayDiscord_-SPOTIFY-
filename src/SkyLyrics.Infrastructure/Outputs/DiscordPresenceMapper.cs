using SkyLyrics.Core.Scene;

namespace SkyLyrics.Infrastructure.Outputs;

public sealed record DiscordActivityPayload(
    string Details,
    string State,
    int Type,
    long? StartedAtUnixMilliseconds);

public static class DiscordPresenceMapper
{
    public static DiscordActivityPayload? Map(SceneState scene, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var track = scene.Track;
        if (track is null ||
            string.IsNullOrWhiteSpace(track.Title) ||
            scene.PlaybackStatus is not (PlaybackStatus.Playing or PlaybackStatus.Paused))
        {
            return null;
        }

        var artist = string.IsNullOrWhiteSpace(track.Artist)
            ? "Artist unavailable"
            : track.Artist.Trim();
        var state = scene.PlaybackStatus == PlaybackStatus.Paused
            ? $"{artist} · Paused"
            : artist;

        long? startedAt = null;
        if (scene.PlaybackStatus == PlaybackStatus.Playing)
        {
            var elapsed = Math.Max(0L, (long)scene.Position.TotalMilliseconds);
            startedAt = now.ToUnixTimeMilliseconds() - elapsed;
        }

        return new DiscordActivityPayload(
            Limit(track.Title.Trim()),
            Limit(state),
            Type: 2,
            StartedAtUnixMilliseconds: startedAt);
    }
    private static string Limit(string value) =>
        value.Length <= 128 ? value : value[..127] + "…";
}
