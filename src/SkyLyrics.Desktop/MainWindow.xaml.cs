using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.Win32;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Models;
using SkyLyrics.Windows;
using SkyLyrics.Persistence;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly SpotifySessionMonitor _spotify = new();
    private readonly DiscordRpcClient _discord = new();
    private readonly SqliteLyricsCache _lyricsCache = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyLyrics", "lyrics.db"));
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly LrclibClient _lyricsClient;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _discordRetryTimer =
        new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly List<TextBlock> _lyricLineBlocks = new();
    private readonly List<Border> _lyricLineSurfaces = new();
    private readonly List<List<Run>> _lyricWordRuns = new();
    private readonly DispatcherTimer _scrollAnimationTimer =
        new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };

    private string? _lastTrackKey;
    private SpotifyTrackSnapshot? _currentTrack;
    private LyricsDocument? _lyrics;
    private LyricsDocument? _renderedLyrics;
    private int? _activeLineIndex;
    private int? _activeWordLineIndex;
    private int? _activeWordIndex;
    private TimeSpan _lastPosition;
    private DateTimeOffset _lastProgressUiUpdate = DateTimeOffset.MinValue;
    private DateTimeOffset _scrollStartedAt = DateTimeOffset.MinValue;
    private double _scrollStartOffset;
    private double _scrollTargetOffset;
    private bool? _lastPlaybackAnimationState;
    private bool? _lastConnectionAnimationState;
    private bool _isDark = true;
    private string? _discordApplicationId;
    private bool _discordEnabled;
    private DateTimeOffset _lastDiscordConnectAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset _manualScrollUntil = DateTimeOffset.MinValue;
    private AppearanceSettings _appearance = AppearanceSettings.Default;
    private GameModeWindow? _gameModeWindow;

    public MainWindow()
    {
        _lyricsClient = new LrclibClient(_httpClient);
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        _discordRetryTimer.Tick += DiscordRetryTimer_Tick;
        _scrollAnimationTimer.Tick += ScrollAnimationTimer_Tick;
        LyricsScrollViewer.PreviewMouseWheel += LyricsScrollViewer_PreviewMouseWheel;
        LyricsScrollViewer.PreviewKeyDown += LyricsScrollViewer_PreviewKeyDown;
        Activated += (_, _) => RefreshPlaybackMotion();
        Deactivated += (_, _) => RefreshPlaybackMotion();
        StateChanged += (_, _) => RefreshPlaybackMotion();
        Loaded += MainWindow_Loaded;
        Closed += async (_, _) =>
        {
            _discordRetryTimer.Stop();
            _timer.Stop();
            _scrollAnimationTimer.Stop();
            _gameModeWindow?.Close();
            _gameModeWindow = null;
            _spotify.SnapshotChanged -= Spotify_SnapshotChanged;
            _spotify.Dispose();

            try
            {
                await _discord.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discord shutdown cleanup failed: {ex}");
            }

            _httpClient.Dispose();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PlayEntranceAnimation();
        _appearance = AppearanceSettingsStore.Load();
        ApplyAppearance();

        _discordApplicationId = DiscordApplicationIdStore.Load();
        if (DiscordApplicationIdStore.IsValidApplicationId(_discordApplicationId))
        {
            _discordEnabled = true;
            DiscordButton.Content = "Disconnect";
            _discordRetryTimer.Start();
        }
        else
        {
            DiscordStatusText.Text = "Discord not configured";
            DiscordButton.Content = "Set up";
        }

        try
        {
            await _lyricsCache.InitializeAsync();
            await _lyricsCache.RemoveExpiredAsync();
        }
        catch (Exception ex)
        {
            ConnectionText.Text = $"Local lyrics cache unavailable: {ex.Message}";
        }

        try
        {
            _spotify.SnapshotChanged += Spotify_SnapshotChanged;
            await _spotify.InitializeAsync();
            ConnectionText.Text = "Spotify session monitor ready";
            _timer.Start();
            await SyncDiscordPresenceAsync();
        }
        catch (Exception ex)
        {
            ConnectionText.Text = $"Media session unavailable: {ex.Message}";
        }
    }

    private void Spotify_SnapshotChanged(SpotifyTrackSnapshot? snapshot)
    {
        Dispatcher.BeginInvoke(new Action(async () => await ApplySnapshotAsync(snapshot)));
    }

    private async Task ApplySnapshotAsync(SpotifyTrackSnapshot? track)
    {
        if (track is null)
        {
            _currentTrack = null;
            _lastTrackKey = null;
            ConnectionText.Text = "Spotify Desktop not detected";
            PlaybackText.Text = "SPOTIFY NOT DETECTED";
            TrackTitle.Text = "No Spotify track detected";
            TrackArtist.Text = "SkyLyrics monitors Spotify Desktop through Windows media sessions.";
            PositionText.Text = "—";
            TrackProgress.Value = 0;
            UpdateGameModeLine(null);
            RefreshPlaybackMotion();
            UpdateRenderInterval();
            await SyncDiscordPresenceAsync();
            return;
        }

        var trackChanged = !string.Equals(_lastTrackKey, track.MatchKey, StringComparison.Ordinal);
        if (trackChanged)
        {
            _scrollAnimationTimer.Stop();
            _manualScrollUntil = DateTimeOffset.MinValue;
            LyricsScrollViewer.ScrollToTop();
        }

        _currentTrack = track;
        _lastPosition = GetEstimatedPosition(track);
        ConnectionText.Text = "Spotify Desktop connected";
        TrackTitle.Text = track.Title;
        TrackArtist.Text = track.Artist;
        PlaybackText.Text = track.IsPlaying ? "PLAYING" : "PAUSED";

        RefreshPlaybackMotion();
        await SyncDiscordPresenceAsync();

        if (trackChanged)
        {
            _lastTrackKey = track.MatchKey;
            AnimateTrackIdentity();
            await LoadLyricsForTrackAsync(track);
        }

        UpdateRenderInterval();
        RenderLyrics(_lastPosition);
        UpdateProgressDisplay(track, _lastPosition);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_currentTrack is not { } track)
            return;

        var position = GetEstimatedPosition(track);
        _lastPosition = position;

        // Lyrics are evaluated on a light render clock. Spotify metadata is event-driven
        // and is never fetched from this timer.
        RenderLyrics(position);

        if (DateTimeOffset.UtcNow - _lastProgressUiUpdate >= TimeSpan.FromMilliseconds(250))
            UpdateProgressDisplay(track, position);
    }

    private static TimeSpan GetEstimatedPosition(SpotifyTrackSnapshot track) =>
        PlaybackPositionEstimator.Estimate(track, DateTimeOffset.UtcNow);

    private async void DiscordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_discordEnabled)
        {
            _discordEnabled = false;
            _discordRetryTimer.Stop();
            await _discord.DisconnectAsync();
            DiscordButton.Content = "Connect";
            DiscordStatusText.Text = "Disconnected";
            UpdateDiscordIndicator(false);
            return;
        }

        if (!DiscordApplicationIdStore.IsValidApplicationId(_discordApplicationId))
        {
            var setup = new DiscordSetupDialog(_discordApplicationId) { Owner = this };
            if (setup.ShowDialog() != true)
                return;

            _discordApplicationId = setup.ApplicationId;
            DiscordApplicationIdStore.Save(_discordApplicationId);
        }

        _discordEnabled = true;
        _lastDiscordConnectAttempt = DateTimeOffset.MinValue;
        DiscordButton.Content = "Disconnect";
        _discordRetryTimer.Start();
        await SyncDiscordPresenceAsync();
    }

    private async void DiscordSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var setup = new DiscordSetupDialog(_discordApplicationId) { Owner = this };
        if (setup.ShowDialog() != true)
            return;

        var nextApplicationId = setup.ApplicationId;
        if (!string.Equals(_discordApplicationId, nextApplicationId, StringComparison.Ordinal))
        {
            if (_discordEnabled)
                await _discord.DisconnectAsync();

            _discordApplicationId = nextApplicationId;
            _lastDiscordConnectAttempt = DateTimeOffset.MinValue;
        }

        DiscordApplicationIdStore.Save(nextApplicationId);

        if (_discordEnabled)
        {
            await SyncDiscordPresenceAsync();
        }
        else
        {
            DiscordStatusText.Text = "Application ID saved";
            UpdateDiscordIndicator(false);
        }
    }

    private async void DiscordRetryTimer_Tick(object? sender, EventArgs e)
    {
        await SyncDiscordPresenceAsync();
    }

    private async Task SyncDiscordPresenceAsync()
    {
        if (!_discordEnabled || !DiscordApplicationIdStore.IsValidApplicationId(_discordApplicationId))
            return;

        if (!_discord.IsConnected &&
            DateTimeOffset.UtcNow - _lastDiscordConnectAttempt < TimeSpan.FromSeconds(6))
            return;

        if (!_discord.IsConnected)
            _lastDiscordConnectAttempt = DateTimeOffset.UtcNow;

        try
        {
            string? currentLyric = null;
            if (_lyrics?.HasLineTimestamps == true &&
                _activeLineIndex is int activeIndex &&
                activeIndex >= 0 &&
                activeIndex < _lyrics.Lines.Count)
            {
                currentLyric = _lyrics.Lines[activeIndex].Text;
            }

            var presence = _currentTrack is null
                ? null
                : DiscordPresenceData.FromTrack(_currentTrack, DateTimeOffset.UtcNow, currentLyric);

            await _discord.SetActivityAsync(_discordApplicationId!, presence);

            DiscordStatusText.Text = _currentTrack is null
                ? "Connected · idle"
                : "Rich Presence active";
            UpdateDiscordIndicator(true);
        }
        catch (Exception ex)
        {
            DiscordStatusText.Text = "Discord unavailable";
            UpdateDiscordIndicator(false);
            System.Diagnostics.Debug.WriteLine($"Discord Rich Presence update failed: {ex}");
        }
    }

    private void UpdateDiscordIndicator(bool connected)
    {
        var color = connected
            ? ThemeColor("AccentBrush", Color.FromRgb(103, 215, 176))
            : ThemeColor("TextMutedBrush", Color.FromRgb(115, 132, 123));

        DiscordConnectionDot.BeginAnimation(UIElement.OpacityProperty, null);
        DiscordConnectionDot.Opacity = connected ? 1 : 0.72;
        DiscordConnectionDot.Fill = new SolidColorBrush(color);
    }

    private void UpdateProgressDisplay(SpotifyTrackSnapshot track, TimeSpan position)
    {
        PositionText.Text = $"{FormatTime(position)} / {(track.Duration is { } duration ? FormatTime(duration) : "—")}";
        TrackProgress.Value = track.Duration is { } total && total > TimeSpan.Zero
            ? Math.Clamp(position.TotalMilliseconds / total.TotalMilliseconds * 100, 0, 100)
            : 0;
        _lastProgressUiUpdate = DateTimeOffset.UtcNow;
    }

    private void UpdateRenderInterval()
    {
        var hasTimedLyrics = _lyrics?.HasLineTimestamps == true;
        var isPlaying = _currentTrack?.IsPlaying == true;

        _timer.Interval = isPlaying && hasTimedLyrics
            ? TimeSpan.FromMilliseconds(33)
            : TimeSpan.FromMilliseconds(400);
    }

    private async Task LoadLyricsForTrackAsync(SpotifyTrackSnapshot track)
    {
        _lyrics = null;
        _renderedLyrics = null;
        _activeLineIndex = null;
        _activeWordLineIndex = null;
        _activeWordIndex = null;
        UpdateGameModeLine(null);
        _lyricLineBlocks.Clear();
        _lyricLineSurfaces.Clear();
        _lyricWordRuns.Clear();
        LyricsPanel.Children.Clear();
        LyricsStateText.Text = "SEARCHING";

        try
        {
            var cached = await _lyricsCache.GetAsync(track);
            if (cached is not null)
            {
                _lyrics = cached;
                LyricsStateText.Text = cached.HasWordTimestamps
                    ? "CACHED · WORD + LINE SYNC"
                    : cached.HasLineTimestamps
                        ? "CACHED · LINE SYNC"
                        : "CACHED · UNSYNCED";
                RenderLyrics(track.Position);
                return;
            }
        }
        catch
        {
            // A cache failure must not prevent live provider lookup.
        }

        AddMessage("Looking for a verified LRCLIB match…");

        try
        {
            var result = await _lyricsClient.GetExactAsync(
                track.Title, track.Artist, track.Album, track.Duration);

            if (result is null)
            {
                LyricsStateText.Text = "NOT FOUND";
                AddMessage("No exact LRCLIB result was found. Import a matching .lrc file to continue.");
                return;
            }

            var match = LyricsMatchValidator.Evaluate(track, result);
            if (!match.IsMatch)
            {
                LyricsStateText.Text = "MATCH REJECTED";
                AddMessage($"LRCLIB candidate was not displayed: {match.Reason}");
                AddMessage("Import a verified LRC or Lyricsfile for this track instead.");
                return;
            }

            var wordDataInvalid = false;

            if (result.HasWordSync && !string.IsNullOrWhiteSpace(result.LyricsFile))
            {
                try
                {
                    _lyrics = LyricsFileParser.Parse(result.LyricsFile, "LRCLIB Lyricsfile");
                }
                catch (FormatException) when (!string.IsNullOrWhiteSpace(result.SyncedLyrics))
                {
                    // Fall back to the provider's LRC timing without manufacturing word timings.
                    _lyrics = LrcParser.Parse(result.SyncedLyrics, "LRCLIB");
                    wordDataInvalid = true;
                }
            }

            if (_lyrics is null && !string.IsNullOrWhiteSpace(result.SyncedLyrics))
            {
                _lyrics = LrcParser.Parse(result.SyncedLyrics, "LRCLIB");
            }
            else if (_lyrics is null && !string.IsNullOrWhiteSpace(result.PlainLyrics))
            {
                _lyrics = LyricsDocument.FromPlainText(result.PlainLyrics, result.TrackName, result.ArtistName);
            }
            else if (_lyrics is null && result.Instrumental)
            {
                LyricsStateText.Text = "INSTRUMENTAL";
                AddMessage("LRCLIB identifies this track as instrumental.");
                return;
            }
            else if (_lyrics is null)
            {
                LyricsStateText.Text = "NO LYRICS";
                AddMessage("The LRCLIB result contains no lyric text.");
                return;
            }

            LyricsStateText.Text = wordDataInvalid
                ? "LINE SYNC · WORD DATA INVALID"
                : _lyrics.HasWordTimestamps
                    ? "WORD + LINE SYNC"
                    : _lyrics.HasLineTimestamps
                        ? "LINE SYNC"
                        : "UNSYNCED";

            await SaveLyricsToCacheAsync(track, _lyrics);
            RenderLyrics(track.Position);
        }
        catch (HttpRequestException ex)
        {
            LyricsStateText.Text = "NETWORK / PROVIDER ERROR";
            AddMessage($"Lyrics provider request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            LyricsStateText.Text = "REQUEST TIMED OUT";
            AddMessage("LRCLIB did not respond before the request timeout.");
        }
        catch (Exception ex)
        {
            LyricsStateText.Text = "LYRICS ERROR";
            AddMessage($"Could not parse the provider response: {ex.Message}");
        }
    }

    private void BuildLyricsSurface()
    {
        if (_lyrics is null)
            return;

        LyricsPanel.Children.Clear();
        _lyricLineBlocks.Clear();
        _lyricLineSurfaces.Clear();
        _lyricWordRuns.Clear();
        _scrollAnimationTimer.Stop();

        if (_lyrics.IsUnsynced)
            AddMessage("UNSYNCED · This source does not provide timestamps.");

        foreach (var line in _lyrics.Lines)
        {
            var block = new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                Foreground = MutedLyricsBrush(),
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                RenderTransform = new TranslateTransform(0, 5),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            var wordRuns = new List<Run>();
            if (line.Words is { Count: > 0 } words &&
                string.Equals(string.Concat(words.Select(word => word.Text)), line.Text, StringComparison.Ordinal))
            {
                foreach (var word in words)
                {
                    var run = new Run(word.Text) { Foreground = MutedLyricsBrush() };
                    wordRuns.Add(run);
                    block.Inlines.Add(run);
                }
            }
            else
            {
                // If source word segments do not reconstruct the source line exactly,
                // preserve the line text and disable word highlighting.
                block.Text = line.Text;
            }

            var surface = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(4, 2, 4, 2),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = block
            };

            _lyricLineBlocks.Add(block);
            _lyricLineSurfaces.Add(surface);
            _lyricWordRuns.Add(wordRuns);
            LyricsPanel.Children.Add(surface);
        }

        _renderedLyrics = _lyrics;
        _activeLineIndex = int.MinValue;
        _activeWordLineIndex = int.MinValue;
        _activeWordIndex = int.MinValue;
        RevealLyricsSurface();
    }

    private void RenderLyrics(TimeSpan position, bool animate = true)
    {
        if (_lyrics is null)
            return;

        if (!ReferenceEquals(_renderedLyrics, _lyrics))
            BuildLyricsSurface();

        var activeIndex = LyricsSyncEngine.FindActiveLineIndex(_lyrics, position);
        var activeWordIndex = activeIndex is int wordLineIndex
            ? LyricsSyncEngine.FindActiveWordIndex(_lyrics.Lines[wordLineIndex], position)
            : null;
        var lineChanged = _activeLineIndex != activeIndex;
        var wordChanged = _activeWordLineIndex != activeIndex || _activeWordIndex != activeWordIndex;

        UpdateGameModeLine(activeIndex);
        if (!lineChanged && !wordChanged)
            return;

        if (lineChanged)
            QueueDiscordPresenceUpdate();

        if (lineChanged)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(300));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var accent = ThemeColor("AccentBrush", Color.FromRgb(103, 215, 176));
            var accentSoft = ThemeColor("AccentSoftBrush", Color.FromRgb(29, 56, 46));
            var muted = MutedLyricsColor();
            var previousActiveIndex = _activeLineIndex;

            for (var index = 0; index < _lyricLineBlocks.Count; index++)
            {
                var block = _lyricLineBlocks[index];
                var surface = _lyricLineSurfaces[index];
                var isActive = activeIndex == index;
                var wasActive = previousActiveIndex == index;
                var shouldAnimate = animate && (isActive || wasActive);
                var shouldUpdateColor = !animate || isActive || wasActive;
                var targetOpacity = isActive ? 1.0 : activeIndex is not null && index < activeIndex ? 0.48 : 0.68;
                var targetFontSize = isActive ? 23.0 : 17.0;
                var targetOffset = isActive ? 0.0 : 4.0;
                var targetTextColor = isActive ? accent : muted;
                var targetSurfaceColor = isActive
                    ? Color.FromArgb(_isDark ? (byte)48 : (byte)92, accentSoft.R, accentSoft.G, accentSoft.B)
                    : Color.FromArgb(0, accentSoft.R, accentSoft.G, accentSoft.B);

                var transforms = AppearanceCatalog.EnsureTransforms(block);
                var animationPreset = AppearanceCatalog.GetAnimation(_appearance.AnimationId);
                var lineDuration = new Duration(TimeSpan.FromMilliseconds(animationPreset.DurationMs));

                if (shouldAnimate)
                {
                    block.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(block.Opacity, targetOpacity, lineDuration)
                        { EasingFunction = AppearanceCatalog.CreateEasing(animationPreset.Easing) });
                    block.BeginAnimation(TextBlock.FontSizeProperty,
                        new DoubleAnimation(block.FontSize, targetFontSize, lineDuration)
                        { EasingFunction = easing });

                    if (isActive)
                    {
                        AppearanceCatalog.AnimateActiveLine(transforms, _appearance.AnimationId);
                    }
                    else
                    {
                        transforms.Move.BeginAnimation(TranslateTransform.YProperty,
                            new DoubleAnimation(transforms.Move.Y, targetOffset, duration) { EasingFunction = easing });
                        transforms.Move.BeginAnimation(TranslateTransform.XProperty,
                            new DoubleAnimation(transforms.Move.X, 0, duration) { EasingFunction = easing });
                        transforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                            new DoubleAnimation(transforms.Scale.ScaleX, 1, duration) { EasingFunction = easing });
                        transforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                            new DoubleAnimation(transforms.Scale.ScaleY, 1, duration) { EasingFunction = easing });
                        transforms.Rotate.BeginAnimation(RotateTransform.AngleProperty,
                            new DoubleAnimation(transforms.Rotate.Angle, 0, duration) { EasingFunction = easing });
                    }
                }
                else
                {
                    block.BeginAnimation(UIElement.OpacityProperty, null);
                    block.BeginAnimation(TextBlock.FontSizeProperty, null);
                    transforms.Move.BeginAnimation(TranslateTransform.YProperty, null);
                    transforms.Move.BeginAnimation(TranslateTransform.XProperty, null);
                    transforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    transforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    transforms.Rotate.BeginAnimation(RotateTransform.AngleProperty, null);
                    block.Opacity = targetOpacity;
                    block.FontSize = targetFontSize;
                    transforms.Move.X = 0;
                    transforms.Move.Y = targetOffset;
                    transforms.Scale.ScaleX = 1;
                    transforms.Scale.ScaleY = 1;
                    transforms.Rotate.Angle = 0;
                }

                block.Effect = isActive
                    ? AppearanceCatalog.CreateTextEffect(_appearance.EffectId)
                    : null;

                if (shouldUpdateColor)
                {
                    if (shouldAnimate)
                    {
                        TransitionTextColor(block, targetTextColor, duration, easing);
                        TransitionSurfaceColor(surface, targetSurfaceColor, duration, easing);
                    }
                    else
                    {
                        block.Foreground = new SolidColorBrush(targetTextColor);
                        surface.Background = new SolidColorBrush(targetSurfaceColor);
                        surface.BorderBrush = new SolidColorBrush(Color.FromArgb(isActive ? (byte)75 : (byte)0, accent.R, accent.G, accent.B));
                    }
                }

                block.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            }

            _activeLineIndex = activeIndex;

            if (activeIndex is int visibleIndex &&
                visibleIndex >= 0 &&
                visibleIndex < _lyricLineSurfaces.Count)
            {
                SmoothScrollToLine(_lyricLineSurfaces[visibleIndex]);
            }
        }

        if (wordChanged)
            UpdateWordHighlights(activeIndex, activeWordIndex);
    }

    private void UpdateWordHighlights(int? activeLineIndex, int? activeWordIndex)
    {
        var muted = MutedLyricsColor();
        var accent = ThemeColor("AccentBrush", Color.FromRgb(103, 215, 176));
        var duration = new Duration(TimeSpan.FromMilliseconds(125));
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        if (_activeWordLineIndex is int previousLine &&
            _activeWordIndex is int previousWord &&
            previousLine >= 0 && previousLine < _lyricWordRuns.Count &&
            previousWord >= 0 && previousWord < _lyricWordRuns[previousLine].Count)
        {
            TransitionRunColor(_lyricWordRuns[previousLine][previousWord], muted, duration, easing);
        }

        if (activeLineIndex is int lineIndex &&
            activeWordIndex is int wordIndex &&
            lineIndex >= 0 && lineIndex < _lyricWordRuns.Count &&
            wordIndex >= 0 && wordIndex < _lyricWordRuns[lineIndex].Count)
        {
            TransitionRunColor(_lyricWordRuns[lineIndex][wordIndex], accent, duration, easing);
        }

        _activeWordLineIndex = activeLineIndex;
        _activeWordIndex = activeWordIndex;
    }

    private void TransitionTextColor(
        TextBlock block,
        Color target,
        Duration duration,
        IEasingFunction easing)
    {
        var current = BrushColor(block.Foreground, MutedLyricsColor());
        var brush = new SolidColorBrush(current);
        block.Foreground = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(current, target, duration) { EasingFunction = easing });
    }

    private static void TransitionRunColor(
        Run run,
        Color target,
        Duration duration,
        IEasingFunction easing)
    {
        var current = BrushColor(run.Foreground, target);
        var brush = new SolidColorBrush(current);
        run.Foreground = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(current, target, duration) { EasingFunction = easing });
    }

    private void TransitionSurfaceColor(
        Border surface,
        Color target,
        Duration duration,
        IEasingFunction easing)
    {
        var current = BrushColor(surface.Background, Colors.Transparent);
        var background = new SolidColorBrush(current);
        surface.Background = background;
        background.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(current, target, duration) { EasingFunction = easing });

        var accent = ThemeColor("AccentBrush", Color.FromRgb(103, 215, 176));
        var currentBorder = BrushColor(surface.BorderBrush, Colors.Transparent);
        var border = new SolidColorBrush(currentBorder);
        surface.BorderBrush = border;
        border.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(currentBorder,
                Color.FromArgb(target.A == 0 ? (byte)0 : (byte)82, accent.R, accent.G, accent.B),
                duration) { EasingFunction = easing });
    }

    private void RevealLyricsSurface()
    {
        LyricsPanel.Opacity = 0;
        LyricsPanel.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
            {
                BeginTime = TimeSpan.FromMilliseconds(40),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void SmoothScrollToLine(Border lineSurface)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded || lineSurface.ActualHeight <= 0 || LyricsScrollViewer.ViewportHeight <= 0)
                return;

            try
            {
                var lineCenter = lineSurface.TranslatePoint(
                    new Point(0, lineSurface.ActualHeight / 2), LyricsPanel).Y;
                if (DateTimeOffset.UtcNow < _manualScrollUntil)
                    return;

                // lineCenter is already in content coordinates; do not add VerticalOffset.
                var target = Math.Clamp(
                    lineCenter - LyricsScrollViewer.ViewportHeight * 0.48,
                    0,
                    LyricsScrollViewer.ScrollableHeight);

                _scrollStartOffset = LyricsScrollViewer.VerticalOffset;
                _scrollTargetOffset = target;
                _scrollStartedAt = DateTimeOffset.UtcNow;

                if (Math.Abs(_scrollTargetOffset - _scrollStartOffset) > 2)
                {
                    _scrollAnimationTimer.Stop();
                    _scrollAnimationTimer.Start();
                }
            }
            catch (InvalidOperationException)
            {
                // Layout can change while a song is switching; the next lyric tick will retry.
            }
        }), DispatcherPriority.Loaded);
    }

    private void LyricsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _manualScrollUntil = DateTimeOffset.UtcNow.AddSeconds(5);
        _scrollAnimationTimer.Stop();
    }

    private void LyricsScrollViewer_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.PageUp or System.Windows.Input.Key.PageDown or
            System.Windows.Input.Key.Home or System.Windows.Input.Key.End or
            System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)
        {
            _manualScrollUntil = DateTimeOffset.UtcNow.AddSeconds(5);
            _scrollAnimationTimer.Stop();
        }
    }

    private void QueueDiscordPresenceUpdate()
    {
        Dispatcher.BeginInvoke(new Action(async () => await SyncDiscordPresenceAsync()),
            DispatcherPriority.Background);
    }

    private void UpdateGameModeLine(int? activeIndex)
    {
        if (_gameModeWindow is null)
            return;

        var line = _lyrics?.HasLineTimestamps == true &&
                   activeIndex is int index &&
                   index >= 0 &&
                   index < _lyrics.Lines.Count
            ? _lyrics.Lines[index].Text
            : null;

        _gameModeWindow.UpdateLyric(line);
    }

    private void CustomizeButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CustomizationWindow(_appearance, settings =>
        {
            _appearance = settings;
            AppearanceSettingsStore.Save(settings);
            ApplyAppearance();
            if (_gameModeWindow is not null)
                _gameModeWindow.ApplyAppearance(settings);
        })
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void GameModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_gameModeWindow is not null)
        {
            _gameModeWindow.Close();
            _gameModeWindow = null;
            GameModeButton.Content = "Game mode";
            return;
        }

        _gameModeWindow = new GameModeWindow(_appearance) { Owner = this };
        _gameModeWindow.Closed += (_, _) =>
        {
            _gameModeWindow = null;
            GameModeButton.Content = "Game mode";
        };
        _gameModeWindow.Show();
        GameModeButton.Content = "Exit game mode";
        UpdateGameModeLine(_activeLineIndex);
    }

    private void ApplyAppearance()
    {
        RootGrid.Background = AppearanceCatalog.CreateBackgroundBrush(_appearance.BackgroundId);
        LyricsCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 20,
            ShadowDepth = 5,
            Opacity = _isDark ? .24 : .10,
            RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
        };

        foreach (var block in _lyricLineBlocks)
            block.Effect = null;

        if (_activeLineIndex is int activeIndex &&
            activeIndex >= 0 &&
            activeIndex < _lyricLineBlocks.Count)
        {
            _lyricLineBlocks[activeIndex].Effect =
                AppearanceCatalog.CreateTextEffect(_appearance.EffectId);
        }
    }

    private void ScrollAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTimeOffset.UtcNow - _scrollStartedAt;
        var progress = Math.Clamp(elapsed.TotalMilliseconds / 280.0, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        var offset = _scrollStartOffset + (_scrollTargetOffset - _scrollStartOffset) * eased;

        LyricsScrollViewer.ScrollToVerticalOffset(offset);

        if (progress >= 1)
        {
            LyricsScrollViewer.ScrollToVerticalOffset(_scrollTargetOffset);
            _scrollAnimationTimer.Stop();
        }
    }

    private static Color BrushColor(Brush? brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;

    private static Color ThemeColor(string key, Color fallback)
    {
        return Application.Current.TryFindResource(key) is SolidColorBrush brush
            ? brush.Color
            : fallback;
    }

    private Color MutedLyricsColor() =>
        _isDark
            ? Color.FromRgb(170, 185, 178)
            : Color.FromRgb(93, 108, 101);

    private Brush MutedLyricsBrush() => new SolidColorBrush(MutedLyricsColor());

    private async Task SaveLyricsToCacheAsync(SpotifyTrackSnapshot track, LyricsDocument lyrics)
    {
        try
        {
            await _lyricsCache.SetAsync(track, lyrics);
        }
        catch (Exception ex)
        {
            // Display/cache failure is non-fatal; the matched lyrics remain in memory.
            LyricsStateText.Text += " · CACHE SAVE FAILED";
            System.Diagnostics.Debug.WriteLine($"SkyLyrics cache write failed: {ex}");
        }
    }

    private void AddMessage(string message)
    {
        var text = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Opacity = 0,
            Foreground = new SolidColorBrush(_isDark ? Color.FromRgb(139, 153, 149) : Color.FromRgb(100, 112, 106)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 10),
            RenderTransform = new TranslateTransform(0, 7)
        };

        LyricsPanel.Children.Add(text);
        text.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        if (text.RenderTransform is TranslateTransform transform)
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import lyrics file",
            Filter = "LRC lyric files (*.lrc)|*.lrc|Lyricsfile YAML (*.lyricsfile.yaml;*.yaml;*.yml)|*.lyricsfile.yaml;*.yaml;*.yml|Text files (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var content = await File.ReadAllTextAsync(dialog.FileName);
            _lyrics = dialog.FilterIndex switch
            {
                1 => LrcParser.Parse(content, "local .lrc"),
                2 => LyricsFileParser.Parse(content, "local Lyricsfile"),
                _ => LyricsDocument.FromPlainText(content, source: "local text")
            };

            _renderedLyrics = null;
            _activeLineIndex = null;
            _activeWordLineIndex = null;
            _activeWordIndex = null;
            _lastPosition = _currentTrack is not null ? GetEstimatedPosition(_currentTrack) : TimeSpan.Zero;
            LyricsStateText.Text = _lyrics.HasWordTimestamps
                ? "LOCAL · WORD + LINE SYNC"
                : _lyrics.HasLineTimestamps
                    ? "LOCAL · LINE SYNC"
                    : "LOCAL · UNSYNCED";

            if (_currentTrack is not null)
                await SaveLyricsToCacheAsync(_currentTrack, _lyrics);

            RenderLyrics(_lastPosition);
            AnimateImportSuccess();
        }
        catch (Exception ex)
        {
            _lyrics = null;
            _renderedLyrics = null;
            _lyricLineBlocks.Clear();
            _lyricLineSurfaces.Clear();
            _lyricWordRuns.Clear();
            LyricsStateText.Text = "IMPORT FAILED";
            LyricsPanel.Children.Clear();
            AddMessage($"Could not import this file: {ex.Message}");
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        ApplyTheme();

        if (_lyrics is not null)
        {
            _renderedLyrics = null;
            RenderLyrics(_lastPosition, animate: false);
        }
    }

    private void ApplyTheme()
    {
        var palette = _isDark
            ? new Dictionary<string, Color>
            {
                ["WindowBackgroundBrush"] = Color.FromRgb(12, 18, 16),
                ["SurfaceBrush"] = Color.FromRgb(19, 28, 25),
                ["SurfaceElevatedBrush"] = Color.FromRgb(25, 36, 31),
                ["StrokeBrush"] = Color.FromRgb(38, 53, 46),
                ["TextPrimaryBrush"] = Color.FromRgb(238, 245, 241),
                ["TextSecondaryBrush"] = Color.FromRgb(167, 182, 174),
                ["TextMutedBrush"] = Color.FromRgb(115, 132, 123),
                ["AccentBrush"] = Color.FromRgb(103, 215, 176),
                ["AccentSoftBrush"] = Color.FromRgb(29, 56, 46),
                ["ButtonSurfaceBrush"] = Color.FromRgb(27, 43, 36),
                ["ButtonForegroundBrush"] = Color.FromRgb(234, 247, 240),
                ["ProgressTrackBrush"] = Color.FromRgb(38, 53, 46)
            }
            : new Dictionary<string, Color>
            {
                ["WindowBackgroundBrush"] = Color.FromRgb(242, 246, 243),
                ["SurfaceBrush"] = Color.FromRgb(255, 255, 255),
                ["SurfaceElevatedBrush"] = Color.FromRgb(247, 251, 248),
                ["StrokeBrush"] = Color.FromRgb(222, 232, 225),
                ["TextPrimaryBrush"] = Color.FromRgb(24, 37, 31),
                ["TextSecondaryBrush"] = Color.FromRgb(82, 100, 90),
                ["TextMutedBrush"] = Color.FromRgb(116, 133, 123),
                ["AccentBrush"] = Color.FromRgb(22, 134, 94),
                ["AccentSoftBrush"] = Color.FromRgb(226, 244, 234),
                ["ButtonSurfaceBrush"] = Color.FromRgb(230, 242, 235),
                ["ButtonForegroundBrush"] = Color.FromRgb(23, 74, 55),
                ["ProgressTrackBrush"] = Color.FromRgb(222, 232, 225)
            };

        var previousWindowColor = BrushColor(Background, _isDark
            ? Color.FromRgb(242, 246, 243)
            : Color.FromRgb(12, 18, 16));

        foreach (var (key, color) in palette)
            Application.Current.Resources[key] = new SolidColorBrush(color);

        var windowBrush = new SolidColorBrush(previousWindowColor);
        Background = windowBrush;
        windowBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(previousWindowColor, palette["WindowBackgroundBrush"],
                new Duration(TimeSpan.FromMilliseconds(260)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        Foreground = (Brush)FindResource("TextPrimaryBrush");
        RootGrid.Background = AppearanceCatalog.CreateBackgroundBrush(_appearance.BackgroundId);
        ThemeButton.Content = _isDark ? "Light theme" : "Dark theme";
    }

    private void PlayEntranceAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootGrid.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(430)) { EasingFunction = easing });

        if (RootGrid.RenderTransform is TranslateTransform rootTransform)
            rootTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(480)) { EasingFunction = easing });

        AnimateSurfaceEntrance(NowPlayingCard, 90, 430);
        AnimateSurfaceEntrance(LyricsCard, 180, 480);
    }

    private static void AnimateSurfaceEntrance(FrameworkElement surface, double delay, double duration)
    {
        surface.Opacity = 0;
        surface.RenderTransform = CreateSurfaceTransform(11);
        surface.RenderTransformOrigin = new Point(0.5, 0.5);

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        surface.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = easing
            });

        if (surface.RenderTransform is TransformGroup group &&
            group.Children.OfType<TranslateTransform>().FirstOrDefault() is { } transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(11, 0, TimeSpan.FromMilliseconds(duration))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delay),
                    EasingFunction = easing
                });
        }
    }

    private static TransformGroup CreateSurfaceTransform(double initialOffset)
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new TranslateTransform(0, initialOffset));
        return group;
    }

    private void SurfaceCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
        AnimateSurfaceScale(sender, 1.006);

    private void SurfaceCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        AnimateSurfaceScale(sender, 1.0);

    private static void AnimateSurfaceScale(object sender, double targetScale)
    {
        if (sender is not FrameworkElement surface ||
            surface.RenderTransform is not TransformGroup group ||
            group.Children.OfType<ScaleTransform>().FirstOrDefault() is not { } scale)
            return;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale.ScaleX, targetScale, TimeSpan.FromMilliseconds(170)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale.ScaleY, targetScale, TimeSpan.FromMilliseconds(170)) { EasingFunction = easing });
    }

    private void AnimateTrackIdentity()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        TrackTitle.Opacity = 0;
        TrackArtist.Opacity = 0.35;

        TrackTitle.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(330)) { EasingFunction = easing });
        TrackArtist.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(45),
                EasingFunction = easing
            });

        if (TrackTitle.RenderTransform is TranslateTransform titleTransform)
            titleTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = easing });
        else
        {
            var transform = new TranslateTransform(0, 7);
            TrackTitle.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = easing });
        }
    }

    private void SetConnectionAnimation(bool connected)
    {
        if (_lastConnectionAnimationState == connected)
            return;

        _lastConnectionAnimationState = connected;
        ConnectionDot.BeginAnimation(UIElement.OpacityProperty, null);
        ConnectionDot.Opacity = 1;

        if (connected)
        {
            ConnectionDot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(1050))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
        }
    }

    private void RefreshPlaybackMotion()
    {
        var windowCanAnimate = IsActive && WindowState != WindowState.Minimized;
        SetConnectionAnimation(windowCanAnimate && _currentTrack is not null);
        SetPlaybackAnimations(windowCanAnimate && _currentTrack?.IsPlaying == true);
    }

    private void SetPlaybackAnimations(bool isPlaying)
    {
        if (_lastPlaybackAnimationState == isPlaying)
            return;

        _lastPlaybackAnimationState = isPlaying;
        var bars = new[] { EqualizerBar1, EqualizerBar2, EqualizerBar3, EqualizerBar4 };

        PlaybackDot.BeginAnimation(UIElement.OpacityProperty, null);
        PlaybackDot.Opacity = 1;

        if (isPlaying)
        {
            PlaybackEqualizer.Visibility = Visibility.Visible;
            PlaybackDot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(720))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });

            for (var index = 0; index < bars.Length; index++)
            {
                if (bars[index].RenderTransform is not ScaleTransform transform)
                    continue;

                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.22 + (index % 2) * 0.12, 0.96,
                        TimeSpan.FromMilliseconds(310 + index * 95))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(index * 75),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });
            }
        }
        else
        {
            PlaybackEqualizer.Visibility = Visibility.Collapsed;
            foreach (var bar in bars)
            {
                if (bar.RenderTransform is ScaleTransform transform)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    transform.ScaleY = 0.42;
                }
            }
        }
    }

    private void AnimateImportSuccess()
    {
        var target = ThemeColor("AccentBrush", Color.FromRgb(103, 215, 176));
        var previous = BrushColor(ImportButtonBrush(), target);
        var flash = new SolidColorBrush(previous);

        if (FindName("ImportButton") is Button importButton)
        {
            importButton.Background = flash;
            var animation = new ColorAnimation(target, TimeSpan.FromMilliseconds(420))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            animation.Completed += (_, _) => importButton.ClearValue(Control.BackgroundProperty);
            flash.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }

    private Brush ImportButtonBrush() =>
        FindName("ImportButton") is Button button ? button.Background : Brushes.Transparent;

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
