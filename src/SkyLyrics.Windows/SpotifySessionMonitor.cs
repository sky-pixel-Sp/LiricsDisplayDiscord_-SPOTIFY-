using SkyLyrics.Core.Models;
using Windows.Media.Control;

namespace SkyLyrics.Windows;

/// <summary>
/// Tracks Spotify through Windows GSMTC events. Metadata is loaded only when a
/// session or its media properties change; timeline updates do not re-fetch metadata.
/// </summary>
public sealed class SpotifySessionMonitor : IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _title;
    private string _artist = string.Empty;
    private string? _album;
    private bool _disposed;

    public event Action<SpotifyTrackSnapshot?>? SnapshotChanged;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
        _manager.SessionsChanged += Manager_SessionsChanged;

        await SelectSpotifySessionAsync();
    }

    private async void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        await SelectSpotifySessionAsync();
    }

    private async void Manager_SessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        await SelectSpotifySessionAsync();
    }

    private async Task SelectSpotifySessionAsync()
    {
        if (_manager is null || _disposed)
            return;

        await _refreshGate.WaitAsync();
        try
        {
            var sessions = _manager.GetSessions()
                .Where(IsSpotifySession)
                .ToArray();

            var current = _manager.GetCurrentSession();
            var selected = sessions.FirstOrDefault(session =>
                    session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? sessions.FirstOrDefault(session => ReferenceEquals(session, current))
                ?? sessions.FirstOrDefault();

            if (!ReferenceEquals(selected, _session))
            {
                DetachSession();
                _session = selected;
                _title = null;
                _artist = string.Empty;
                _album = null;

                if (_session is not null)
                    AttachSession(_session);
            }

            if (_session is null)
            {
                SnapshotChanged?.Invoke(null);
                return;
            }

            await PublishSnapshotCoreAsync(refreshMediaProperties: _title is null);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static bool IsSpotifySession(GlobalSystemMediaTransportControlsSession session) =>
        session.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase);

    private void AttachSession(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
        session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
        session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
    }

    private void DetachSession()
    {
        if (_session is null)
            return;

        _session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        _session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
        _session = null;
    }

    private async void Session_MediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _session))
            await PublishSnapshotAsync(refreshMediaProperties: true);
    }

    private async void Session_PlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _session))
            await PublishSnapshotAsync(refreshMediaProperties: false);
    }

    private async void Session_TimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _session))
            await PublishSnapshotAsync(refreshMediaProperties: false);
    }

    private async Task PublishSnapshotAsync(bool refreshMediaProperties)
    {
        if (_disposed || !await _refreshGate.WaitAsync(0))
            return;

        try
        {
            await PublishSnapshotCoreAsync(refreshMediaProperties);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task PublishSnapshotCoreAsync(bool refreshMediaProperties)
    {
        var session = _session;
        if (session is null)
            return;

        if (refreshMediaProperties || string.IsNullOrWhiteSpace(_title))
        {
            var media = await session.TryGetMediaPropertiesAsync();
            if (media is null || string.IsNullOrWhiteSpace(media.Title))
            {
                SnapshotChanged?.Invoke(null);
                return;
            }

            _title = media.Title;
            _artist = media.Artist ?? string.Empty;
            _album = string.IsNullOrWhiteSpace(media.AlbumTitle) ? null : media.AlbumTitle;
        }

        var timeline = session.GetTimelineProperties();
        var playback = session.GetPlaybackInfo();
        var duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : (TimeSpan?)null;
        var updatedAt = timeline.LastUpdatedTime == default
            ? DateTimeOffset.UtcNow
            : timeline.LastUpdatedTime;

        SnapshotChanged?.Invoke(new SpotifyTrackSnapshot(
            _title!,
            _artist,
            _album,
            timeline.Position,
            duration,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            session.SourceAppUserModelId,
            updatedAt));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            _manager.SessionsChanged -= Manager_SessionsChanged;
        }

        DetachSession();
        _refreshGate.Dispose();
    }
}
