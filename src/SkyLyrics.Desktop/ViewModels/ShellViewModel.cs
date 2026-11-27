using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Application.Playback;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Desktop.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly Version AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 2, 0);

    private readonly IPlaybackSessionSource _playbackSource;
    private readonly ILyricsService _lyricsService;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private PlaybackSnapshot? _snapshot;
    private LyricsTrackSignature? _requestedSignature;
    private CancellationTokenSource? _lyricsCancellation;
    private long _lyricsGeneration;
    private bool _disposed;

    private string _lyricsStatusText = "Waiting for Spotify playback.";
    private string _lyricsModeLabel = "NO LYRICS";
    private string _lyricsText = string.Empty;

    public ShellViewModel(
        IPlaybackSessionSource playbackSource,
        ILyricsService lyricsService,
        ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(playbackSource);
        ArgumentNullException.ThrowIfNull(lyricsService);
        ArgumentNullException.ThrowIfNull(logger);

        _playbackSource = playbackSource;
        _lyricsService = lyricsService;
        _logger = logger;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _snapshot = playbackSource.Current;
        _playbackSource.SnapshotChanged += OnSnapshotChanged;

        if (_snapshot is not null)
        {
            ApplySnapshot(_snapshot, PlaybackSessionChangeKind.Connected);
        }
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

            return "Playback metadata and timeline are received through Windows GSMTC.";
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

    public string LyricsStatusText
    {
        get => _lyricsStatusText;
        private set
        {
            if (_lyricsStatusText == value)
            {
                return;
            }

            _lyricsStatusText = value;
            OnPropertyChanged(nameof(LyricsStatusText));
        }
    }

    public string LyricsModeLabel
    {
        get => _lyricsModeLabel;
        private set
        {
            if (_lyricsModeLabel == value)
            {
                return;
            }

            _lyricsModeLabel = value;
            OnPropertyChanged(nameof(LyricsModeLabel));
        }
    }

    public string LyricsText
    {
        get => _lyricsText;
        private set
        {
            if (_lyricsText == value)
            {
                return;
            }

            _lyricsText = value;
            OnPropertyChanged(nameof(LyricsText));
        }
    }

    public string NextMilestone => "Phase 3 — event-driven SyncScheduler and SceneState";

    public string VersionLabel => $"VERSION {AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";

    public async Task LoadLocalLyricsFileAsync(string filePath)
    {
        if (_disposed)
        {
            return;
        }

        CancelLyricsLookup();
        _requestedSignature = null;
        var generation = Interlocked.Increment(ref _lyricsGeneration);
        var cancellation = new CancellationTokenSource();
        _lyricsCancellation = cancellation;

        LyricsStatusText = "Importing local lyrics file…";
        LyricsModeLabel = "IMPORTING";

        try
        {
            var result = await _lyricsService.LoadLocalFileAsync(filePath, cancellation.Token)
                .ConfigureAwait(false);
            DispatchLyricsResult(generation, result, null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Local lyrics import failed for {FilePath}.", filePath);
            DispatchLyricsResult(generation, null, "The selected lyrics file could not be imported.");
        }
    }

    private void OnSnapshotChanged(object? sender, PlaybackSessionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            ApplySnapshot(e.Snapshot, e.ChangeKind);
            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(new Action(() => ApplySnapshot(e.Snapshot, e.ChangeKind)));
        }
    }

    private void ApplySnapshot(PlaybackSnapshot? snapshot, PlaybackSessionChangeKind changeKind)
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

        if (snapshot is null)
        {
            CancelLyricsLookup();
            _requestedSignature = null;
            LyricsModeLabel = "NO LYRICS";
            LyricsText = string.Empty;
            LyricsStatusText = "Waiting for Spotify playback.";
            return;
        }

        var signature = CreateSignature(snapshot);
        if (signature is null)
        {
            CancelLyricsLookup();
            _requestedSignature = null;
            LyricsModeLabel = "NO MATCH";
            LyricsText = string.Empty;
            LyricsStatusText = "Track title or artist metadata is missing; lookup was skipped.";
            return;
        }

        if (!Equals(_requestedSignature, signature) ||
            changeKind is PlaybackSessionChangeKind.Connected or PlaybackSessionChangeKind.TrackChanged)
        {
            if (!Equals(_requestedSignature, signature) || _lyricsCancellation is null)
            {
                StartLyricsLookup(signature);
            }
        }
    }

    private void StartLyricsLookup(LyricsTrackSignature signature)
    {
        CancelLyricsLookup();
        _requestedSignature = signature;

        var cancellation = new CancellationTokenSource();
        _lyricsCancellation = cancellation;
        var generation = Interlocked.Increment(ref _lyricsGeneration);

        LyricsStatusText = "Checking local cache and LRCLIB…";
        LyricsModeLabel = "LOOKING UP";
        LyricsText = string.Empty;

        _ = LoadLyricsAsync(signature, generation, cancellation.Token);
    }

    private async Task LoadLyricsAsync(
        LyricsTrackSignature signature,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _lyricsService.GetExactAsync(signature, cancellationToken).ConfigureAwait(false);
            DispatchLyricsResult(generation, result, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "LRCLIB lookup failed for track {TrackTitle} by {TrackArtist}.",
                signature.Title,
                signature.Artist);
            DispatchLyricsResult(generation, null, "Lyrics lookup failed. Check the network connection and application log.");
        }
    }

    private void DispatchLyricsResult(
        long generation,
        LyricsLookupResult? result,
        string? error)
    {
        if (_disposed || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            ApplyLyricsResult(generation, result, error);
            return;
        }

        _dispatcher.BeginInvoke(new Action(() => ApplyLyricsResult(generation, result, error)));
    }

    private void ApplyLyricsResult(
        long generation,
        LyricsLookupResult? result,
        string? error)
    {
        if (_disposed || generation != Interlocked.Read(ref _lyricsGeneration))
        {
            return;
        }

        if (error is not null)
        {
            LyricsModeLabel = "LOOKUP FAILED";
            LyricsText = string.Empty;
            LyricsStatusText = error;
            return;
        }

        if (result is null)
        {
            LyricsModeLabel = "NO MATCH";
            LyricsText = string.Empty;
            LyricsStatusText = "LRCLIB did not return a matching lyrics record.";
            return;
        }

        var document = result.Document;
        LyricsModeLabel = document.SyncMode switch
        {
            LyricsSyncMode.WordSynced => "WORD-SYNCED LYRICS",
            LyricsSyncMode.LineSynced => "LINE-SYNCED LYRICS",
            LyricsSyncMode.Unsynced => "UNSYNCED — NO TIMESTAMPS",
            LyricsSyncMode.Instrumental => "INSTRUMENTAL",
            _ => "LYRICS"
        };

        LyricsText = document.Instrumental
            ? string.Empty
            : document.SyncMode == LyricsSyncMode.Unsynced && document.PlainText is not null
                ? document.PlainText
                : string.Join(Environment.NewLine, document.Lines.Select(static line => line.Text));

        var sourceDescription = result.Source switch
        {
            LyricsLookupSource.Cache => "Local cache",
            LyricsLookupSource.LocalFile => "Local file",
            _ => result.SourceName
        };

        LyricsStatusText = document.Instrumental
            ? $"Instrumental classification received from {sourceDescription}."
            : $"Loaded from {sourceDescription}.";

        if (!document.Diagnostics.IsEmpty)
        {
            LyricsStatusText += $"  ·  {document.Diagnostics.Length} source warning(s)";
        }
    }

    private static LyricsTrackSignature? CreateSignature(PlaybackSnapshot snapshot)
    {
        var title = snapshot.Track?.Title;
        var artist = snapshot.Track?.Artist;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        TimeSpan? duration = null;
        if (snapshot.EndTime is { } endTime)
        {
            var candidate = endTime - snapshot.StartTime;
            if (candidate > TimeSpan.Zero && candidate <= TimeSpan.FromHours(1))
            {
                duration = candidate;
            }
        }

        return new LyricsTrackSignature(
            title.Trim(),
            artist.Trim(),
            snapshot.Track?.Album,
            duration);
    }

    private void CancelLyricsLookup()
    {
        Interlocked.Increment(ref _lyricsGeneration);
        var previous = _lyricsCancellation;
        _lyricsCancellation = null;

        if (previous is null)
        {
            return;
        }

        previous.Cancel();
        previous.Dispose();
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
        CancelLyricsLookup();
    }
}
