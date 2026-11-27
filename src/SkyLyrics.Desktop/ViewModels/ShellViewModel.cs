using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using SkyLyrics.Application.Playback;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Desktop.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly Version AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);

    private readonly IPlaybackSessionSource _playbackSource;
    private readonly Dispatcher _dispatcher;
    private PlaybackSnapshot? _snapshot;
    private bool _disposed;

    public ShellViewModel(IPlaybackSessionSource playbackSource)
    {
        ArgumentNullException.ThrowIfNull(playbackSource);

        _playbackSource = playbackSource;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _snapshot = playbackSource.Current;
        _playbackSource.SnapshotChanged += OnSnapshotChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SessionBadgeText =>
        _snapshot is null ? "SPOTIFY NOT DETECTED" : "SPOTIFY CONNECTED";

    public string StatusTitle
    {
        get
        {
            var title = _snapshot?.Track?.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return _snapshot is null ? "Waiting for Spotify Desktop" : "Spotify session connected";
        }
    }

    public string ArtistLine
    {
        get
        {
            var values = new[]
            {
                _snapshot?.Track?.Artist,
                _snapshot?.Track?.Album
            };

            var summary = string.Join(
                "  ·  ",
                values.Where(static value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(summary)
                ? "Track metadata has not been supplied."
                : summary;
        }
    }

    public string StatusDescription
    {
        get
        {
            if (_snapshot is null)
            {
                return "Start Spotify Desktop and begin playback. SkyLyrics is listening for its Windows media session; browser player sessions are not selected.";
            }

            if (string.IsNullOrWhiteSpace(_snapshot.Track?.Title))
            {
                return "Spotify Desktop is connected, but Windows has not supplied a track title yet.";
            }

            return "Playback metadata and timeline are received through Windows GSMTC. Lyrics and synchronization are introduced in later phases.";
        }
    }

    public string PlaybackStatusLabel => _snapshot?.PlaybackStatus switch
    {
        PlaybackStatus.Playing => "PLAYING",
        PlaybackStatus.Paused => "PAUSED",
        PlaybackStatus.Stopped => "STOPPED",
        _ => _snapshot is null ? "WAITING FOR SESSION" : "SESSION AVAILABLE"
    };

    public string PositionLabel
    {
        get
        {
            if (_snapshot is null)
            {
                return "NO TIMELINE";
            }

            var current = FormatTime(_snapshot.Position);
            return _snapshot.EndTime is { } end
                ? $"{current}  /  {FormatTime(end)}"
                : current;
        }
    }

    public string SourceIdentityLabel =>
        _snapshot is null
            ? "SOURCE  —"
            : $"SOURCE  {_snapshot.SourceAppUserModelId}";

    public string NextMilestone => "Phase 2 — LRCLIB provider, LRC parser and SQLite cache";

    public string VersionLabel => $"VERSION {AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";

    private void OnSnapshotChanged(object? sender, PlaybackSessionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            ApplySnapshot(e.Snapshot);
            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(new Action(() => ApplySnapshot(e.Snapshot)));
        }
    }

    private void ApplySnapshot(PlaybackSnapshot? snapshot)
    {
        if (_disposed)
        {
            return;
        }

        _snapshot = snapshot;
        OnPropertyChanged(nameof(SessionBadgeText));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(ArtistLine));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(PlaybackStatusLabel));
        OnPropertyChanged(nameof(PositionLabel));
        OnPropertyChanged(nameof(SourceIdentityLabel));
    }

    private static string FormatTime(TimeSpan value)
    {
        var totalMinutes = (long)value.TotalMinutes;
        return $"{totalMinutes:00}:{value.Seconds:00}";
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _playbackSource.SnapshotChanged -= OnSnapshotChanged;
    }
}
