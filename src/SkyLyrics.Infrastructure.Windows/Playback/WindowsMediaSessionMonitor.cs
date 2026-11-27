using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Playback;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SkyLyrics.Infrastructure.Windows.Playback;

/// <summary>
/// Publishes one selected, supported Windows media session through the application's
/// shared playback clock. Browser sessions remain labelled as browser sessions because
/// GSMTC does not expose a reliable website identity.
/// </summary>
public sealed class WindowsMediaSessionMonitor : BackgroundService, IPlaybackSessionSource
{
    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<WindowsMediaSessionMonitor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly PlaybackClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _activeSession;
    private TrackMetadata? _track;
    private PlaybackSnapshot? _current;
    private CancellationToken _stoppingToken;

    public WindowsMediaSessionMonitor(
        ILogger<WindowsMediaSessionMonitor> logger,
        TimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _clock = new PlaybackClock(timeProvider);
    }

    public PlaybackSnapshot? Current => Volatile.Read(ref _current);

    public event EventHandler<PlaybackSessionChangedEventArgs>? SnapshotChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var reconnectDelay = InitialReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager
                    .RequestAsync()
                    .AsTask(stoppingToken)
                    .ConfigureAwait(false);

                _manager.SessionsChanged += OnSessionsChanged;
                _manager.CurrentSessionChanged += OnCurrentSessionChanged;

                _logger.LogInformation("Windows GSMTC media session manager is ready.");
                await RefreshSelectedSessionAsync(
                    PlaybackSessionChangeKind.Connected,
                    refreshMediaProperties: true,
                    stoppingToken).ConfigureAwait(false);

                reconnectDelay = InitialReconnectDelay;
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Windows GSMTC session manager failed. Retrying in {Delay}.", reconnectDelay);
            }
            finally
            {
                DetachSession();
                DetachManager();
                UpdateAvailableSessions([]);
                _clock.Reset();
                PublishSnapshot(null, PlaybackSessionChangeKind.Disconnected);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(reconnectDelay, stoppingToken).ConfigureAwait(false);
            reconnectDelay = TimeSpan.FromMilliseconds(
                Math.Min(reconnectDelay.TotalMilliseconds * 2d, MaximumReconnectDelay.TotalMilliseconds));
        }
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) =>
        QueueRefresh(PlaybackSessionChangeKind.Connected, refreshMediaProperties: true);

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        QueueRefresh(PlaybackSessionChangeKind.Connected, refreshMediaProperties: true);

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) =>
        QueueRefresh(PlaybackSessionChangeKind.TrackChanged, refreshMediaProperties: true);

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) =>
        QueueRefresh(PlaybackSessionChangeKind.PlaybackChanged, refreshMediaProperties: false);

    private void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) =>
        QueueRefresh(PlaybackSessionChangeKind.TimelineChanged, refreshMediaProperties: false);

    private void QueueRefresh(
        PlaybackSessionChangeKind changeKind,
        bool refreshMediaProperties)
    {
        _ = RefreshFromEventAsync(changeKind, refreshMediaProperties);
    }

    private async Task RefreshFromEventAsync(
        PlaybackSessionChangeKind changeKind,
        bool refreshMediaProperties)
    {
        try
        {
            await RefreshSelectedSessionAsync(
                changeKind,
                refreshMediaProperties,
                _stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Windows media-session event refresh failed.");
        }
    }

    private async Task RefreshSelectedSessionAsync(
        PlaybackSessionChangeKind requestedKind,
        bool refreshMediaProperties,
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manager = _manager;
            if (manager is null)
            {
                PublishSnapshot(null, PlaybackSessionChangeKind.Disconnected);
                return;
            }

            var sessions = manager.GetSessions()
                .Where(session => MediaSessionIdentityMatcher
                    .Resolve(session.SourceAppUserModelId)
                    .IsSupported)
                .ToArray();

            var selectedSession = SelectSession(manager, sessions);
            if (selectedSession is null)
            {
                var hadSession = _activeSession is not null || _current is not null;
                DetachSession();
                _track = null;
                _clock.Reset();

                if (hadSession)
                {
                    PublishSnapshot(null, PlaybackSessionChangeKind.Disconnected);
                }

                return;
            }

            var sessionChanged = !Equals(_activeSession, selectedSession);
            if (sessionChanged)
            {
                DetachSession();
                _activeSession = selectedSession;
                _activeSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _activeSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _activeSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                _track = null;
                _clock.Reset();
                refreshMediaProperties = true;
            }

            await PublishActiveSessionAsync(
                selectedSession,
                requestedKind,
                refreshMediaProperties,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static GlobalSystemMediaTransportControlsSession? SelectSession(
        GlobalSystemMediaTransportControlsSessionManager manager,
        IReadOnlyList<GlobalSystemMediaTransportControlsSession> supportedSessions)
    {
        if (supportedSessions.Count == 0)
        {
            return null;
        }

        var currentSession = manager.GetCurrentSession();
        var playingSessions = supportedSessions
            .Where(session => session.GetPlaybackInfo().PlaybackStatus ==
                              GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            .ToArray();

        if (currentSession is not null && playingSessions.Any(session => Equals(session, currentSession)))
        {
            return currentSession;
        }

        if (playingSessions.Length > 0)
        {
            return playingSessions[0];
        }

        if (currentSession is not null &&
            supportedSessions.Any(session => Equals(session, currentSession)))
        {
            return currentSession;
        }

        return supportedSessions[0];
    }

    private async Task PublishActiveSessionAsync(
        GlobalSystemMediaTransportControlsSession session,
        PlaybackSessionChangeKind requestedKind,
        bool refreshMediaProperties,
        CancellationToken cancellationToken)
    {
        if (refreshMediaProperties || _track is null)
        {
            try
            {
                var mediaProperties = await session.TryGetMediaPropertiesAsync()
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);

                var artwork = await ReadArtworkAsync(mediaProperties.Thumbnail, cancellationToken)
                    .ConfigureAwait(false);

                _track = new TrackMetadata(
                    mediaProperties.Title,
                    mediaProperties.Artist,
                    mediaProperties.AlbumTitle,
                    mediaProperties.AlbumArtist,
                    artwork);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogDebug(exception, "The selected Windows media session did not return metadata.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var timeline = session.GetTimelineProperties();
        var playbackInfo = session.GetPlaybackInfo();
        var playbackStatus = MapPlaybackStatus(playbackInfo.PlaybackStatus);
        var playbackRate = playbackInfo.PlaybackRate ?? 1d;
        var startTime = timeline.StartTime < TimeSpan.Zero ? TimeSpan.Zero : timeline.StartTime;
        TimeSpan? endTime = timeline.EndTime > startTime ? timeline.EndTime : null;

        _clock.Update(
            timeline.Position,
            timeline.LastUpdatedTime,
            playbackRate,
            playbackStatus,
            startTime,
            endTime);

        var snapshot = new PlaybackSnapshot(
            session.SourceAppUserModelId,
            _track,
            playbackStatus,
            _clock.GetPosition(),
            startTime,
            endTime,
            timeline.MinSeekTime,
            timeline.MaxSeekTime,
            _clock.PlaybackRate,
            timeline.LastUpdatedTime,
            _timeProvider.GetUtcNow());

        var current = _current;
        var kind = requestedKind;

        if (current is null)
        {
            kind = PlaybackSessionChangeKind.Connected;
        }
        else if (!SameTrackMetadata(current.Track, snapshot.Track) ||
                 !StringComparer.OrdinalIgnoreCase.Equals(
                     current.SourceAppUserModelId,
                     snapshot.SourceAppUserModelId) ||
                 requestedKind == PlaybackSessionChangeKind.TrackChanged)
        {
            kind = PlaybackSessionChangeKind.TrackChanged;
        }
        else if (current.PlaybackStatus != snapshot.PlaybackStatus)
        {
            kind = PlaybackSessionChangeKind.PlaybackChanged;
        }

        PublishSnapshot(snapshot, kind);
    }

    private const int MaximumArtworkBytes = 8 * 1024 * 1024;

    private async Task<ImmutableArray<byte>> ReadArtworkAsync(
        IRandomAccessStreamReference? thumbnail,
        CancellationToken cancellationToken)
    {
        if (thumbnail is null)
        {
            return ImmutableArray<byte>.Empty;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (stream.Size == 0 || stream.Size > MaximumArtworkBytes)
            {
                _logger.LogDebug(
                    "Media artwork was skipped because its payload size {ArtworkSize} is outside the supported range.",
                    stream.Size);
                return ImmutableArray<byte>.Empty;
            }

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);
            return ImmutableArray.CreateRange(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "The active Windows media session did not expose readable artwork.");
            return ImmutableArray<byte>.Empty;
        }
    }

    private static bool SameTrackMetadata(TrackMetadata? left, TrackMetadata? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return StringComparer.Ordinal.Equals(left.Title, right.Title) &&
               StringComparer.Ordinal.Equals(left.Artist, right.Artist) &&
               StringComparer.Ordinal.Equals(left.Album, right.Album) &&
               StringComparer.Ordinal.Equals(left.AlbumArtist, right.AlbumArtist);
    }

    private void PublishSnapshot(
        PlaybackSnapshot? snapshot,
        PlaybackSessionChangeKind changeKind)
    {
        Volatile.Write(ref _current, snapshot);

        var handlers = SnapshotChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new PlaybackSessionChangedEventArgs(snapshot, changeKind);
        foreach (EventHandler<PlaybackSessionChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "A playback snapshot subscriber failed.");
            }
        }
    }

    private void DetachSession()
    {
        if (_activeSession is null)
        {
            return;
        }

        _activeSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _activeSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _activeSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _activeSession = null;
    }

    private void DetachManager()
    {
        if (_manager is null)
        {
            return;
        }

        _manager.SessionsChanged -= OnSessionsChanged;
        _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        _manager = null;
    }

    private static PlaybackStatus MapPlaybackStatus(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status) =>
        status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            _ => PlaybackStatus.Unknown
        };
}
