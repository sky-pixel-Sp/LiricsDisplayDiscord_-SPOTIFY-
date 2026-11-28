using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Playback;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Desktop.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "2.1.0-preview.1";

    private readonly IPlaybackSessionSource _playbackSource;
    private readonly IAppPreferencesStore _preferences;
    private readonly ILyricsService _lyricsService;
    private readonly SyncScheduler _syncScheduler;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private PlaybackSnapshot? _snapshot;
    private SceneState _sceneState = SceneState.Empty;
    private LyricsDocument? _activeLyricsDocument;
    private LyricsDocument? _activeTranslationDocument;
    private CancellationTokenSource? _translationCancellation;
    private long _translationGeneration;
    private string _translationStatusText = "No translation attached to this track.";
    private ImmutableArray<byte> _artworkBytes;
    private ImageSource? _albumArtwork;
    private LyricsTrackSignature? _requestedSignature;
    private CancellationTokenSource? _lyricsCancellation;
    private long _lyricsGeneration;
    private bool _disposed;

    private string _lyricsStatusText = "Waiting for media playback.";
    private string _lyricsModeLabel = "NO LYRICS";
    private string _lyricsText = string.Empty;

    public ShellViewModel(
        IPlaybackSessionSource playbackSource,
        ILyricsService lyricsService,
        SyncScheduler syncScheduler,
        IAppPreferencesStore preferences,
        ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(playbackSource);
        ArgumentNullException.ThrowIfNull(lyricsService);
        ArgumentNullException.ThrowIfNull(syncScheduler);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(logger);

        _playbackSource = playbackSource;
        _lyricsService = lyricsService;
        _preferences = preferences;
        _syncScheduler = syncScheduler;
        _sceneState = syncScheduler.Current;
        _syncScheduler.SceneChanged += OnSceneChanged;
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
        _snapshot is null
            ? "NO MEDIA SESSION"
            : $"{MediaSessionIdentityMatcher.Resolve(_snapshot.SourceAppUserModelId).DisplayName.ToUpperInvariant()} CONNECTED";

    public string StatusTitle
    {
        get
        {
            var title = _snapshot?.Track?.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return _snapshot is null ? "Waiting for music playback" : "Media session connected";
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
                return "Start Spotify Desktop, a browser music player, or Windows Media. SkyLyrics will use a supported Windows media session.";
            }

            if (string.IsNullOrWhiteSpace(_snapshot.Track?.Title))
            {
                return "The media session is connected, but Windows has not supplied a track title yet.";
            }

            return "Playback metadata and timeline are received through Windows GSMTC.";
        }
    }

    public string PlaybackStatusLabel => _snapshot?.PlaybackStatus switch
    {
        PlaybackStatus.Playing => "PLAYING",
        PlaybackStatus.Paused => "PAUSED",
        PlaybackStatus.Stopped => "STOPPED",
        _ => _snapshot is null ? "WAITING FOR MEDIA SESSION" : "SESSION AVAILABLE"
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
            : $"SOURCE  {MediaSessionIdentityMatcher.Resolve(_snapshot.SourceAppUserModelId).DisplayName}";

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

    public SceneState CurrentScene => _sceneState;

    public string PreviousLyricText => _sceneState.PreviousLine?.Text ?? string.Empty;

    public string ActiveLyricText => _sceneState.ActiveLine?.Text ?? string.Empty;

    public string UpcomingLyricText => _sceneState.NextLine?.Text ?? string.Empty;

    public bool HasPreviousLyric => !string.IsNullOrWhiteSpace(PreviousLyricText);

    public bool HasActiveLine => _sceneState.ActiveLine is not null;

    public bool ShowStageEmptyState => !HasActiveLine;

    public bool HasUpcomingLyric => !string.IsNullOrWhiteSpace(UpcomingLyricText);

    public bool HasActiveWord => !string.IsNullOrWhiteSpace(ActiveWordText);

    public ImageSource? AlbumArtwork
    {
        get => _albumArtwork;
        private set
        {
            if (ReferenceEquals(_albumArtwork, value))
            {
                return;
            }

            _albumArtwork = value;
            OnPropertyChanged(nameof(AlbumArtwork));
            OnPropertyChanged(nameof(HasAlbumArtwork));
            OnPropertyChanged(nameof(ArtworkAvailabilityLabel));
        }
    }

    public bool HasAlbumArtwork => _albumArtwork is not null;

    public string ArtworkAvailabilityLabel => HasAlbumArtwork
        ? "ARTWORK · WINDOWS MEDIA SESSION"
        : "ARTWORK NOT PROVIDED BY MEDIA SESSION";

    public string StageEmptyStateText
    {
        get
        {
            if (_snapshot is null)
            {
                return "Connect a supported music session to begin.";
            }

            if (_activeLyricsDocument is null)
            {
                return _lyricsStatusText;
            }

            return _activeLyricsDocument.SyncMode switch
            {
                LyricsSyncMode.Unsynced =>
                    "These lyrics have no source timestamps. Playback highlighting is unavailable.",
                LyricsSyncMode.Instrumental =>
                    "Instrumental recording · no vocal lyrics supplied.",
                _ when _sceneState.NextLine is not null =>
                    "Waiting for the next source lyric timestamp.",
                _ => "No lyric line is active at this playback position."
            };
        }
    }

    public LyricsDocument? ActiveLyricsDocument => _activeLyricsDocument;

    public bool HasTranslation => _activeTranslationDocument is not null;

    public bool HasTranslationDisplay => !string.IsNullOrWhiteSpace(TranslationDisplayText);

    public string TranslationModeLabel => _activeTranslationDocument is null
        ? string.Empty
        : $"TRANSLATION · {_activeTranslationDocument.Language ?? "Unspecified language"} · " +
          (_activeTranslationDocument.SyncMode switch
          {
              LyricsSyncMode.LineSynced => "LINE-SYNCED",
              LyricsSyncMode.WordSynced => "TIMED SOURCE",
              LyricsSyncMode.Unsynced => "UNSYNCED",
              LyricsSyncMode.Instrumental => "INSTRUMENTAL",
              _ => "LOCAL"
          });

    public string TranslationStatusText => _translationStatusText;

    public string TranslationFullText => _activeTranslationDocument is not { } document || document.Instrumental
        ? string.Empty
        : document.PlainText ?? string.Join(
            Environment.NewLine,
            document.Lines.Select(static line => line.Text));

    public string TranslationDisplayText
    {
        get
        {
            var document = _activeTranslationDocument;
            if (document is null || document.Instrumental)
            {
                return string.Empty;
            }

            if (document.SyncMode is LyricsSyncMode.LineSynced or LyricsSyncMode.WordSynced)
            {
                return document.Lines
                    .Where(line => line.Start is { } start && start <= _sceneState.Position)
                    .OrderBy(static line => line.Start)
                    .LastOrDefault()?.Text ?? string.Empty;
            }

            return document.PlainText ?? string.Join(
                Environment.NewLine,
                document.Lines.Select(static line => line.Text));
        }
    }

    public async Task<bool> LoadTranslationFileAsync(string filePath, string language)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var signature = _requestedSignature ?? (_snapshot is null ? null : CreateSignature(_snapshot));
        if (signature is null)
        {
            SetTranslationStatus("Start a supported media session before attaching a translation.");
            return false;
        }

        CancelTranslationLookup();
        var generation = Interlocked.Increment(ref _translationGeneration);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _translationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        var token = _translationCancellation.Token;
        SetTranslationStatus("Loading local translation…");

        return await LoadTranslationCoreAsync(
            filePath,
            language,
            signature,
            generation,
            token,
            persistPreference: true).ConfigureAwait(false);
    }

    public async Task RemoveTranslationAsync()
    {
        var signature = _requestedSignature ?? (_snapshot is null ? null : CreateSignature(_snapshot));
        if (signature is null)
        {
            ClearTranslationState("No active track translation to remove.");
            return;
        }

        var trackKey = CreateTranslationTrackKey(signature);
        var remaining = _preferences.Current.LocalTranslations
            .Where(profile => !StringComparer.Ordinal.Equals(profile.TrackKey, trackKey))
            .ToImmutableArray();

        try
        {
            await _preferences.SaveAsync(_preferences.Current with { LocalTranslations = remaining })
                .ConfigureAwait(false);
            ClearTranslationState("Local translation removed for this track.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not remove local translation preference.");
            SetTranslationStatus("Could not remove the saved translation reference.");
        }
    }

    private async Task<bool> LoadTranslationCoreAsync(
        string filePath,
        string language,
        LyricsTrackSignature signature,
        long generation,
        CancellationToken cancellationToken,
        bool persistPreference)
    {
        try
        {
            var result = await _lyricsService.LoadLocalFileAsync(filePath, cancellationToken)
                .ConfigureAwait(false);
            var source = result.Document;

            if (!string.IsNullOrWhiteSpace(source.Title) &&
                !StringComparer.Ordinal.Equals(LyricsCacheKey.Normalize(source.Title), signature.NormalizedTitle))
            {
                throw new InvalidDataException("The selected translation title does not match the active track.");
            }

            if (!string.IsNullOrWhiteSpace(source.Artist) &&
                !StringComparer.Ordinal.Equals(LyricsCacheKey.Normalize(source.Artist), signature.NormalizedArtist))
            {
                throw new InvalidDataException("The selected translation artist does not match the active track.");
            }

            var translated = new LyricsDocument(
                source.Title ?? signature.Title,
                source.Artist ?? signature.Artist,
                source.Album ?? signature.Album,
                source.Duration ?? signature.Duration,
                source.Instrumental,
                source.Lines,
                source.PlainText,
                source.Diagnostics,
                language.Trim());

            if (persistPreference)
            {
                var profile = new LocalTranslationPreference(
                    CreateTranslationTrackKey(signature),
                    Path.GetFullPath(filePath),
                    language.Trim());
                var profiles = _preferences.Current.LocalTranslations
                    .Where(existing => !StringComparer.Ordinal.Equals(existing.TrackKey, profile.TrackKey))
                    .Append(profile)
                    .ToImmutableArray();

                await _preferences.SaveAsync(
                    _preferences.Current with { LocalTranslations = profiles },
                    cancellationToken).ConfigureAwait(false);
            }

            if (_disposed ||
                generation != Interlocked.Read(ref _translationGeneration) ||
                !Equals(signature, _requestedSignature ?? (_snapshot is null ? null : CreateSignature(_snapshot))))
            {
                return false;
            }

            DispatchTranslation(generation, translated, null);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Local translation load failed for {FilePath}.", filePath);
            DispatchTranslation(generation, null, "The selected translation could not be loaded or did not match this track.");
            return false;
        }
    }

    private void LoadSavedTranslationForTrack(LyricsTrackSignature signature)
    {
        CancelTranslationLookup();
        var generation = Interlocked.Increment(ref _translationGeneration);
        _activeTranslationDocument = null;
        OnPropertyChanged(nameof(HasTranslation));
        OnPropertyChanged(nameof(HasTranslationDisplay));
        OnPropertyChanged(nameof(TranslationDisplayText));
        OnPropertyChanged(nameof(TranslationFullText));
        OnPropertyChanged(nameof(TranslationModeLabel));

        var key = CreateTranslationTrackKey(signature);
        var saved = _preferences.Current.LocalTranslations
            .FirstOrDefault(profile => StringComparer.Ordinal.Equals(profile.TrackKey, key));
        if (saved is null)
        {
            SetTranslationStatus("No translation attached to this track.");
            return;
        }

        if (!File.Exists(saved.FilePath))
        {
            SetTranslationStatus("The saved local translation file is unavailable. Re-import it to restore this lane.");
            return;
        }

        _translationCancellation = new CancellationTokenSource();
        SetTranslationStatus($"Loading saved {saved.Language} translation…");
        _ = LoadTranslationCoreAsync(
            saved.FilePath,
            saved.Language,
            signature,
            generation,
            _translationCancellation.Token,
            persistPreference: false);
    }

    private void DispatchTranslation(long generation, LyricsDocument? document, string? status)
    {
        if (_disposed || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        void Apply()
        {
            if (_disposed || generation != Interlocked.Read(ref _translationGeneration))
            {
                return;
            }

            _activeTranslationDocument = document;
            SetTranslationStatus(status ?? (document is null
                ? "No translation attached to this track."
                : $"Loaded local {document.Language ?? "unlabelled"} translation."));
            OnPropertyChanged(nameof(HasTranslation));
            OnPropertyChanged(nameof(HasTranslationDisplay));
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(TranslationFullText));
            OnPropertyChanged(nameof(TranslationModeLabel));
        }

        if (_dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _dispatcher.BeginInvoke(new Action(Apply));
        }
    }

    private void SetTranslationStatus(string status)
    {
        if (_dispatcher.CheckAccess())
        {
            _translationStatusText = status;
            OnPropertyChanged(nameof(TranslationStatusText));
            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                _translationStatusText = status;
                OnPropertyChanged(nameof(TranslationStatusText));
            }));
        }
    }

    private void ClearTranslationState(string status)
    {
        CancelTranslationLookup();

        void Apply()
        {
            _activeTranslationDocument = null;
            _translationStatusText = status;
            OnPropertyChanged(nameof(TranslationStatusText));
            OnPropertyChanged(nameof(HasTranslation));
            OnPropertyChanged(nameof(HasTranslationDisplay));
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(TranslationFullText));
            OnPropertyChanged(nameof(TranslationModeLabel));
        }

        if (_dispatcher.CheckAccess())
        {
            Apply();
        }
        else if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(new Action(Apply));
        }
    }

    private void CancelTranslationLookup()
    {
        Interlocked.Increment(ref _translationGeneration);
        var previous = _translationCancellation;
        _translationCancellation = null;
        if (previous is null) return;
        previous.Cancel();
        previous.Dispose();
    }

    private static string CreateTranslationTrackKey(LyricsTrackSignature signature)
    {
        var identity = string.Join(
            "\u001f",
            signature.NormalizedTitle,
            signature.NormalizedArtist,
            signature.NormalizedAlbum);
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string CreateLrcExportText()
    {
        var document = _activeLyricsDocument
            ?? throw new InvalidOperationException("There is no active lyrics document to export.");

        if (document.SyncMode == LyricsSyncMode.WordSynced)
        {
            throw new InvalidOperationException(
                "This document contains word-level timestamps. Exporting it as standard LRC would discard those timings.");
        }

        var hasTimedLines = document.Lines.Any(static line => line.Start is not null);
        if (hasTimedLines && document.Lines.Any(static line => line.Start is null))
        {
            throw new InvalidOperationException(
                "This document mixes timed and untimed lines. Standard LRC cannot preserve that structure safely.");
        }

        var builder = new StringBuilder();
        AppendMetadata(builder, "ti", document.Title);
        AppendMetadata(builder, "ar", document.Artist);
        AppendMetadata(builder, "al", document.Album);

        if (document.Duration is { } duration)
        {
            builder.Append("[length:")
                .Append(FormatLrcTimestamp(duration))
                .AppendLine("]");
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        foreach (var line in document.Lines)
        {
            if (line.Start is { } timestamp)
            {
                builder.Append('[')
                    .Append(FormatLrcTimestamp(timestamp))
                    .Append(']');
            }

            builder.AppendLine(line.Text);
        }

        if (document.Lines.IsEmpty && !string.IsNullOrWhiteSpace(document.PlainText))
        {
            builder.AppendLine(document.PlainText);
        }

        return builder.ToString();
    }

    private static void AppendMetadata(StringBuilder builder, string tag, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append('[').Append(tag).Append(':').Append(value.Trim()).AppendLine("]");
        }
    }

    private static string FormatLrcTimestamp(TimeSpan value)
    {
        var totalMinutes = (long)value.TotalMinutes;
        return $"{totalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
    }

    public string ActiveWordText => _sceneState.WordFill?.Word.Text ?? string.Empty;

    public bool HasTimedActiveWord => _sceneState.WordFill is { Word.End: not null };

    public double ActiveWordProgress => _sceneState.WordFill?.Progress ?? 0d;

    public string ActiveWordProgressLabel =>
        _sceneState.WordFill is { Word.End: not null } fill
            ? $"{Math.Round(fill.Progress * 100d):0}%"
            : string.Empty;

    public string SyncPositionLabel => FormatTime(_sceneState.Position);

    public string SyncStatusLabel => _sceneState.PlaybackStatus switch
    {
        PlaybackStatus.Playing => "SYNC CLOCK · LIVE",
        PlaybackStatus.Paused => "SYNC CLOCK · PAUSED",
        PlaybackStatus.Stopped => "SYNC CLOCK · STOPPED",
        _ => "SYNC CLOCK · WAITING"
    };

    public string NextMilestone => "SkyLyrics 2.1 — multi-source music companion";

    public string VersionLabel => $"VERSION {AppVersion}";

    public bool LoadCachedLyrics(LyricsCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_disposed)
        {
            return false;
        }

        var signature = _snapshot is null ? null : CreateSignature(_snapshot);
        if (signature is null)
        {
            LyricsStatusText = "Start a supported media session before restoring cached lyrics.";
            return false;
        }

        var document = entry.Document;
        if (string.IsNullOrWhiteSpace(document.Title) ||
            string.IsNullOrWhiteSpace(document.Artist) ||
            !StringComparer.Ordinal.Equals(LyricsCacheKey.Normalize(document.Title), signature.NormalizedTitle) ||
            !StringComparer.Ordinal.Equals(LyricsCacheKey.Normalize(document.Artist), signature.NormalizedArtist))
        {
            LyricsStatusText = "This cached record does not match the currently playing track. No lyrics were changed.";
            return false;
        }

        CancelLyricsLookup();
        _requestedSignature = signature;
        _activeLyricsDocument = document;
        _syncScheduler.UpdateLyrics(document);
        OnPropertyChanged(nameof(ActiveLyricsDocument));
        OnPropertyChanged(nameof(StageEmptyStateText));

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
        LyricsStatusText = $"Restored verified cache entry from {entry.Provider} · cached {entry.CachedAtUtc.ToLocalTime():g}.";
        return true;
    }

    public async Task LoadLocalLyricsFileAsync(string filePath)
    {
        if (_disposed)
        {
            return;
        }

        CancelLyricsLookup();
        ClearTranslationState("Translation lane cleared after importing a different source document.");
        _requestedSignature = null;
        var generation = Interlocked.Increment(ref _lyricsGeneration);
        var cancellation = new CancellationTokenSource();
        _lyricsCancellation = cancellation;

        _activeLyricsDocument = null;
        _syncScheduler.UpdateLyrics(null);
        OnPropertyChanged(nameof(StageEmptyStateText));
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

    private static BitmapImage? DecodeArtwork(ImmutableArray<byte> artworkBytes)
    {
        if (artworkBytes.IsDefaultOrEmpty)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(artworkBytes.ToArray(), writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.DecodePixelWidth = 512;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            // Invalid provider artwork is treated as unavailable; no synthetic cover is substituted.
            return null;
        }
    }

    private void OnSceneChanged(object? sender, SceneStateChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            ApplySceneState(e.State);
            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(new Action(() => ApplySceneState(e.State)));
        }
    }

    private void ApplySceneState(SceneState state)
    {
        if (_disposed)
        {
            return;
        }

        _sceneState = state;
        OnPropertyChanged(nameof(CurrentScene));

        var artwork = state.Track?.ArtworkBytes ?? ImmutableArray<byte>.Empty;
        if (!_artworkBytes.Equals(artwork))
        {
            _artworkBytes = artwork;
            AlbumArtwork = DecodeArtwork(artwork);
        }

        OnPropertyChanged(nameof(PreviousLyricText));
        OnPropertyChanged(nameof(ActiveLyricText));
        OnPropertyChanged(nameof(UpcomingLyricText));
        OnPropertyChanged(nameof(HasPreviousLyric));
        OnPropertyChanged(nameof(HasActiveLine));
        OnPropertyChanged(nameof(ShowStageEmptyState));
        OnPropertyChanged(nameof(HasUpcomingLyric));
        OnPropertyChanged(nameof(HasActiveWord));
        OnPropertyChanged(nameof(StageEmptyStateText));
        OnPropertyChanged(nameof(ActiveWordText));
        OnPropertyChanged(nameof(HasTimedActiveWord));
        OnPropertyChanged(nameof(ActiveWordProgress));
        OnPropertyChanged(nameof(ActiveWordProgressLabel));
        OnPropertyChanged(nameof(SyncPositionLabel));
        OnPropertyChanged(nameof(SyncStatusLabel));
        OnPropertyChanged(nameof(TranslationDisplayText));
        OnPropertyChanged(nameof(HasTranslationDisplay));
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
        _syncScheduler.UpdatePlayback(snapshot, changeKind);
        OnPropertyChanged(nameof(SessionBadgeText));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(ArtistLine));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(PlaybackStatusLabel));
        OnPropertyChanged(nameof(PositionLabel));
        OnPropertyChanged(nameof(SourceIdentityLabel));

        if (snapshot is null)
        {
            CancelTranslationLookup();
            _activeTranslationDocument = null;
            SetTranslationStatus("Waiting for a supported media session.");
            OnPropertyChanged(nameof(HasTranslation));
            OnPropertyChanged(nameof(HasTranslationDisplay));
            OnPropertyChanged(nameof(TranslationModeLabel));
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(TranslationFullText));
            CancelLyricsLookup();
            _requestedSignature = null;
            _activeLyricsDocument = null;
            _syncScheduler.UpdateLyrics(null);
            OnPropertyChanged(nameof(StageEmptyStateText));
            LyricsModeLabel = "NO LYRICS";
            LyricsText = string.Empty;
            LyricsStatusText = "Waiting for media playback.";
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
            ClearTranslationState("Translation is unavailable without a stable track identity.");
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

        _activeLyricsDocument = null;
        _syncScheduler.UpdateLyrics(null);
        OnPropertyChanged(nameof(StageEmptyStateText));
        LyricsStatusText = "Checking local cache and LRCLIB…";
        LyricsModeLabel = "LOOKING UP";
        LyricsText = string.Empty;

        LoadSavedTranslationForTrack(signature);
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
            _activeLyricsDocument = null;
            _syncScheduler.UpdateLyrics(null);
            OnPropertyChanged(nameof(StageEmptyStateText));
            LyricsModeLabel = "LOOKUP FAILED";
            LyricsText = string.Empty;
            LyricsStatusText = error;
            return;
        }

        if (result is null)
        {
            _activeLyricsDocument = null;
            _syncScheduler.UpdateLyrics(null);
            OnPropertyChanged(nameof(StageEmptyStateText));
            LyricsModeLabel = "NO MATCH";
            LyricsText = string.Empty;
            LyricsStatusText = "LRCLIB did not return a matching lyrics record.";
            return;
        }

        var document = result.Document;
        _activeLyricsDocument = document;
        _syncScheduler.UpdateLyrics(document);
        OnPropertyChanged(nameof(StageEmptyStateText));
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
        _syncScheduler.SceneChanged -= OnSceneChanged;
        CancelLyricsLookup();
        CancelTranslationLookup();
    }
}
