using SkyLyrics.Core.Models;
using Windows.Media.Control;

namespace SkyLyrics.Windows;

/// <summary>
/// Reads metadata and playback position exposed by Windows GSMTC.
/// It does not read or record audio and does not use Spotify private APIs.
/// </summary>
public sealed class SpotifySessionMonitor
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitializeAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public async Task<SpotifyTrackSnapshot?> GetCurrentTrackAsync()
    {
        if (_manager is null)
            await InitializeAsync();

        var session = _manager!.GetSessions()
            .FirstOrDefault(candidate =>
                candidate.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));

        if (session is null)
            return null;

        var media = await session.TryGetMediaPropertiesAsync();
        if (media is null || string.IsNullOrWhiteSpace(media.Title))
            return null;

        var timeline = session.GetTimelineProperties();
        var playback = session.GetPlaybackInfo();
        var duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : (TimeSpan?)null;

        return new SpotifyTrackSnapshot(
            media.Title,
            media.Artist ?? string.Empty,
            string.IsNullOrWhiteSpace(media.AlbumTitle) ? null : media.AlbumTitle,
            timeline.Position,
            duration,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            session.SourceAppUserModelId);
    }
}
