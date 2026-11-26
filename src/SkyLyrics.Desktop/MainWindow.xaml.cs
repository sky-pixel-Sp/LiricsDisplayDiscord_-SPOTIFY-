using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Models;
using SkyLyrics.Windows;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly SpotifySessionMonitor _spotify = new();
    private readonly LrclibClient _lyricsClient = new(new HttpClient { Timeout = TimeSpan.FromSeconds(12) });
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private string? _lastTrackKey;
    private LyricsDocument? _lyrics;
    private bool _isRefreshing;
    private bool _isDark = true;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
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
                AddMessage("No exact LRCLIB result was found. Import a matching LRC file to continue.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.SyncedLyrics))
            {
                _lyrics = LrcParser.Parse(result.SyncedLyrics, "LRCLIB");
                LyricsStateText.Text = "LINE SYNC";
                if (result.HasWordSync)
                    LyricsStateText.Text = "LINE SYNC · WORD TIMING FILE AVAILABLE";
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

    private void RenderLyrics(TimeSpan position)
    {
        if (_lyrics is null)
            return;

        var activeIndex = LyricsSyncEngine.FindActiveLineIndex(_lyrics, position);
        LyricsPanel.Children.Clear();

        if (_lyrics.IsUnsynced)
            AddMessage("UNSYNCED · This source does not provide timestamps.");

        for (var index = 0; index < _lyrics.Lines.Count; index++)
        {
            var line = _lyrics.Lines[index];
            var isActive = activeIndex == index;
            LyricsPanel.Children.Add(new TextBlock
            {
                Text = line.Text,
                FontSize = isActive ? 21 : 17,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isActive
                    ? (Brush)FindResource("AccentBrush")
                    : new SolidColorBrush(_isDark ? Color.FromRgb(170, 185, 178) : Color.FromRgb(93, 108, 101)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 7, 0, 7)
            });
        }
    }

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

            LyricsStateText.Text = _lyrics.HasLineTimestamps ? "LOCAL · LINE SYNC" : "LOCAL · UNSYNCED";
            RenderLyrics(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
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
            RenderLyrics(TimeSpan.Zero);
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
