using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Models;
using SkyLyrics.Windows;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly SpotifySessionMonitor _spotify = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly LrclibClient _lyricsClient;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly List<TextBlock> _lyricLineBlocks = new();

    private string? _lastTrackKey;
    private LyricsDocument? _lyrics;
    private LyricsDocument? _renderedLyrics;
    private int? _activeLineIndex;
    private TimeSpan _lastPosition;
    private bool _isRefreshing;
    private bool _isDark = true;

    public MainWindow()
    {
        _lyricsClient = new LrclibClient(_httpClient);
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            _timer.Stop();
            _httpClient.Dispose();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _spotify.InitializeAsync();
            ConnectionText.Text = "Spotify session monitor ready";
            _timer.Start();
            await RefreshPlaybackAsync();
        }
        catch (Exception ex)
        {
            ConnectionText.Text = $"Media session unavailable: {ex.Message}";
        }
    }

    private async void Timer_Tick(object? sender, EventArgs e) => await RefreshPlaybackAsync();

    private async Task RefreshPlaybackAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            var track = await _spotify.GetCurrentTrackAsync();
            if (track is null)
            {
                ConnectionText.Text = "Spotify Desktop not detected";
                PlaybackText.Text = "Open Spotify Desktop and start playback";
                TrackTitle.Text = "No Spotify track detected";
                TrackArtist.Text = "SkyLyrics monitors Spotify Desktop through Windows media sessions.";
                PositionText.Text = "—";
                TrackProgress.Value = 0;
                return;
            }

            _lastPosition = track.Position;
            ConnectionText.Text = "Spotify Desktop connected";
            TrackTitle.Text = track.Title;
            TrackArtist.Text = track.Artist;
            PlaybackText.Text = track.IsPlaying ? "PLAYING" : "PAUSED";
            PositionText.Text = $"{FormatTime(track.Position)} / {(track.Duration is { } duration ? FormatTime(duration) : "—")}";
            TrackProgress.Value = track.Duration is { } total && total > TimeSpan.Zero
                ? Math.Clamp(track.Position.TotalMilliseconds / total.TotalMilliseconds * 100, 0, 100)
                : 0;

            if (!string.Equals(_lastTrackKey, track.MatchKey, StringComparison.Ordinal))
            {
                _lastTrackKey = track.MatchKey;
                await LoadLyricsForTrackAsync(track);
            }

            RenderLyrics(track.Position);
        }
        catch (Exception ex)
        {
            ConnectionText.Text = $"Session read error: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task LoadLyricsForTrackAsync(SpotifyTrackSnapshot track)
    {
        _lyrics = null;
        _renderedLyrics = null;
        _activeLineIndex = null;
        _lyricLineBlocks.Clear();
        LyricsPanel.Children.Clear();
        LyricsStateText.Text = "SEARCHING";
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

            if (!string.IsNullOrWhiteSpace(result.SyncedLyrics))
            {
                _lyrics = LrcParser.Parse(result.SyncedLyrics, "LRCLIB");
                LyricsStateText.Text = "LINE SYNC";
            }
            else if (!string.IsNullOrWhiteSpace(result.PlainLyrics))
            {
                _lyrics = LyricsDocument.FromPlainText(result.PlainLyrics, result.TrackName, result.ArtistName);
                LyricsStateText.Text = "UNSYNCED";
            }
            else if (result.Instrumental)
            {
                LyricsStateText.Text = "INSTRUMENTAL";
                AddMessage("LRCLIB identifies this track as instrumental.");
                return;
            }
            else
            {
                LyricsStateText.Text = "NO LYRICS";
                AddMessage("The LRCLIB result contains no lyric text.");
                return;
            }

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

        if (_lyrics.IsUnsynced)
            AddMessage("UNSYNCED · This source does not provide timestamps.");

        foreach (var line in _lyrics.Lines)
        {
            var block = new TextBlock
            {
                Text = line.Text,
                FontSize = 17,
                FontWeight = FontWeights.Normal,
                Foreground = MutedLyricsBrush(),
                Opacity = 0.68,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 7, 0, 7),
                RenderTransform = new TranslateTransform(0, 3),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            _lyricLineBlocks.Add(block);
            LyricsPanel.Children.Add(block);
        }

        _renderedLyrics = _lyrics;
        // Sentinel ensures that the first sync state is applied even when there
        // is no active line yet (for example, before the first LRC timestamp).
        _activeLineIndex = int.MinValue;
    }

    private void RenderLyrics(TimeSpan position, bool animate = true)
    {
        if (_lyrics is null)
            return;

        if (!ReferenceEquals(_renderedLyrics, _lyrics))
            BuildLyricsSurface();

        var activeIndex = LyricsSyncEngine.FindActiveLineIndex(_lyrics, position);
        if (_activeLineIndex == activeIndex)
            return;

        var animationDuration = new Duration(TimeSpan.FromMilliseconds(260));
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        for (var index = 0; index < _lyricLineBlocks.Count; index++)
        {
            var block = _lyricLineBlocks[index];
            var isActive = activeIndex == index;
            var targetOpacity = isActive ? 1.0 : activeIndex is not null && index < activeIndex ? 0.48 : 0.68;
            var targetFontSize = isActive ? 22.0 : 17.0;
            var targetOffset = isActive ? 0.0 : 3.0;

            var currentOpacity = block.Opacity;
            block.Opacity = targetOpacity;
            block.FontSize = targetFontSize;
            block.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            block.Foreground = isActive
                ? (Brush)FindResource("AccentBrush")
                : MutedLyricsBrush();

            if (block.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                block.RenderTransform = transform;
            }

            var currentOffset = transform.Y;
            transform.Y = targetOffset;

            if (animate)
            {
                block.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(currentOpacity, targetOpacity, animationDuration) { EasingFunction = easing });
                transform.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(currentOffset, targetOffset, animationDuration) { EasingFunction = easing });
            }
            else
            {
                block.BeginAnimation(UIElement.OpacityProperty, null);
                transform.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }

        _activeLineIndex = activeIndex;

        if (activeIndex is int visibleIndex && visibleIndex >= 0 && visibleIndex < _lyricLineBlocks.Count)
            _lyricLineBlocks[visibleIndex].BringIntoView();
    }

    private Brush MutedLyricsBrush() =>
        new SolidColorBrush(_isDark
            ? Color.FromRgb(170, 185, 178)
            : Color.FromRgb(93, 108, 101));

    private void AddMessage(string message)
    {
        LyricsPanel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 14,
            Foreground = new SolidColorBrush(_isDark ? Color.FromRgb(139, 153, 149) : Color.FromRgb(100, 112, 106)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        });
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import lyrics file",
            Filter = "LRC lyric files (*.lrc)|*.lrc|Text files (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var content = await File.ReadAllTextAsync(dialog.FileName);
            _lyrics = dialog.FilterIndex == 1
                ? LrcParser.Parse(content, "local .lrc")
                : LyricsDocument.FromPlainText(content, source: "local text");

            _renderedLyrics = null;
            _activeLineIndex = null;
            _lastPosition = TimeSpan.Zero;
            LyricsStateText.Text = _lyrics.HasLineTimestamps ? "LOCAL · LINE SYNC" : "LOCAL · UNSYNCED";
            RenderLyrics(_lastPosition);
        }
        catch (Exception ex)
        {
            _lyrics = null;
            _renderedLyrics = null;
            _lyricLineBlocks.Clear();
            LyricsStateText.Text = "IMPORT FAILED";
            LyricsPanel.Children.Clear();
            AddMessage($"Could not import this file: {ex.Message}");
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        Background = new SolidColorBrush(_isDark ? Color.FromRgb(16, 20, 20) : Color.FromRgb(242, 246, 244));
        Foreground = new SolidColorBrush(_isDark ? Color.FromRgb(242, 245, 244) : Color.FromRgb(29, 42, 37));
        RootGrid.Background = Background;
        ThemeButton.Content = _isDark ? "Light theme" : "Dark theme";
        Resources["AccentBrush"] = new SolidColorBrush(_isDark
            ? Color.FromRgb(88, 214, 198)
            : Color.FromRgb(24, 133, 117));
        UpdateThemeBorders();

        if (_lyrics is not null)
        {
            _renderedLyrics = null;
            RenderLyrics(_lastPosition, animate: false);
        }
    }

    private void UpdateThemeBorders()
    {
        foreach (var border in FindVisualChildren<Border>(RootGrid))
        {
            if (border.CornerRadius != new CornerRadius(18))
                continue;

            border.Background = new SolidColorBrush(_isDark
                ? Color.FromRgb(25, 33, 31)
                : Colors.White);
            border.BorderBrush = new SolidColorBrush(_isDark
                ? Color.FromRgb(44, 57, 53)
                : Color.FromRgb(218, 228, 222));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
