using System.ComponentModel;
using MessageBox = System.Windows.MessageBox;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Microsoft.Win32;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Desktop.Services;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Synchronization;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeService _windowModes;
    private readonly IAppPreferencesStore _preferences;
    private readonly SyncScheduler _scheduler;
    private readonly ISelectablePlaybackSessionSource _playbackSource;
    private VisualStudioWindow? _visualStudioWindow;
    private bool _isFullscreen;
    private WindowState _restoreWindowState;
    private WindowStyle _restoreWindowStyle;
    private ResizeMode _restoreResizeMode;

    public MainWindow(
        ShellViewModel viewModel,
        WindowModeService windowModes,
        IAppPreferencesStore preferences,
        SyncScheduler scheduler,
        ISelectablePlaybackSessionSource playbackSource)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _windowModes = windowModes ?? throw new ArgumentNullException(nameof(windowModes));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _playbackSource = playbackSource ?? throw new ArgumentNullException(nameof(playbackSource));
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closing += OnClosing;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _windowModes.CloseAuxiliaryWindows();
            if (System.Windows.Application.Current is { Dispatcher.HasShutdownStarted: false })
                Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current?.Shutdown()));
        };
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Do not hijack the close button into a hide-to-tray action.
        _windowModes.CloseAuxiliaryWindows();
    }

    public void RequestApplicationExit()
    {
        _windowModes.CloseAuxiliaryWindows();
        Close();
        System.Windows.Application.Current?.Shutdown();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width * 0.94d);
        Height = Math.Min(Height, workArea.Height * 0.90d);
        PlayEntranceAnimation();
    }

    private void PlayEntranceAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation || _preferences.Current.ReduceMotion)
        {
            RootGrid.Opacity = 1d;
            if (RootGrid.RenderTransform is TranslateTransform reduced) reduced.Y = 0d;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        RootGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(520))
        {
            EasingFunction = ease
        });
        if (RootGrid.RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(14d, 0d, TimeSpan.FromMilliseconds(620)) { EasingFunction = ease });
    }

    private void ToggleGameMode_Click(object sender, RoutedEventArgs e) => _windowModes.ToggleGameMode();
    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _restoreWindowState = WindowState;
            _restoreWindowStyle = WindowStyle;
            _restoreResizeMode = ResizeMode;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            FullscreenLabel.Text = "EXIT FULL";
            _isFullscreen = true;
            return;
        }

        WindowState = WindowState.Normal;
        WindowStyle = _restoreWindowStyle;
        ResizeMode = _restoreResizeMode;
        WindowState = _restoreWindowState == WindowState.Minimized ? WindowState.Normal : _restoreWindowState;
        FullscreenLabel.Text = "FULL SCREEN";
        _isFullscreen = false;
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void ShowSettings()
    {
        ShowFromTray();
        new SettingsWindow(_preferences, _scheduler, _playbackSource) { Owner = this }.ShowDialog();
    }

    public void ShowDiagnostics()
    {
        ShowFromTray();
        new DiagnosticsWindow(_viewModel) { Owner = this }.Show();
    }

    private void OpenVisualStudio_Click(object sender, RoutedEventArgs e)
    {
        if (_visualStudioWindow is { IsVisible: true })
        {
            _visualStudioWindow.Activate();
            return;
        }

        var window = new VisualStudioWindow(_viewModel, _preferences) { Owner = this };
        _visualStudioWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_visualStudioWindow, window))
            {
                _visualStudioWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    private void OpenFocus_Click(object sender, RoutedEventArgs e) => _windowModes.ShowFocusView();
    private void ToggleMini_Click(object sender, RoutedEventArgs e) => _windowModes.ToggleMiniPlayer();
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e) => ShowDiagnostics();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveLyricText))
        {
            Dispatcher.BeginInvoke(new Action(AnimateActiveLyric));
        }
        else if (e.PropertyName == nameof(ShellViewModel.AlbumArtwork))
        {
            Dispatcher.BeginInvoke(new Action(AnimateArtwork));
        }
    }

    private void AnimateActiveLyric()
    {
        if (!SystemParameters.ClientAreaAnimation || _preferences.Current.ReduceMotion)
        {
            ActiveLyricTextBlock.BeginAnimation(OpacityProperty, null);
            ActiveLyricTextBlock.Opacity = 1d;
            if (ActiveLyricTextBlock.RenderTransform is TransformGroup reducedGroup)
            {
                var reducedMotionTransform = reducedGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (reducedMotionTransform is not null)
                {
                    reducedMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
                    reducedMotionTransform.Y = 0d;
                }
                var reducedScale = reducedGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (reducedScale is not null)
                {
                    reducedScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    reducedScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    reducedScale.ScaleX = 1d;
                    reducedScale.ScaleY = 1d;
                }
            }

            return;
        }

        ActiveLyricTextBlock.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.18d, 1d, TimeSpan.FromMilliseconds(360))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        if (ActiveLyricTextBlock.RenderTransform is TransformGroup group)
        {
            var transform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (transform is not null)
                transform.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(20d, 0d, TimeSpan.FromMilliseconds(430))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });

            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            if (scale is not null)
            {
                var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.16d };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.965d, 1d, TimeSpan.FromMilliseconds(430)) { EasingFunction = spring });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.965d, 1d, TimeSpan.FromMilliseconds(430)) { EasingFunction = spring });
            }
        }
    }

    private void AnimateArtwork()
    {
        if (!SystemParameters.ClientAreaAnimation || _preferences.Current.ReduceMotion || !_viewModel.HasAlbumArtwork)
        {
            ArtworkBackdrop.BeginAnimation(OpacityProperty, null);
            ArtworkBackdrop.Opacity = _viewModel.HasAlbumArtwork ? 0.15d : 0d;
            return;
        }

        ArtworkBackdrop.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0d, 0.15d, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private async void EditLyrics_Click(object sender, RoutedEventArgs e)
    {
        var document = _viewModel.ActiveLyricsDocument;
        if (document is null)
        {
            MessageBox.Show(
                this,
                "Load a lyrics document before opening the editor.",
                "Lyrics Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (document.SyncMode == SkyLyrics.Core.Lyrics.LyricsSyncMode.WordSynced)
        {
            MessageBox.Show(
                this,
                "This document contains word-level timestamps. The standard LRC editor is disabled for it so those timings cannot be lost. Keep the original word-synced source file.",
                "Word-synced lyrics are protected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string sourceText;
        try
        {
            sourceText = _viewModel.CreateLrcExportText();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Lyrics Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var editor = new LyricsEditorWindow(sourceText, document.Title, document.Artist)
        {
            Owner = this
        };

        if (editor.ShowDialog() == true && !string.IsNullOrWhiteSpace(editor.SavedFilePath))
        {
            await _viewModel.LoadLocalLyricsFileAsync(editor.SavedFilePath);
        }
    }

    private async void ImportTranslation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import a local translation file",
            Filter = "Lyrics files|*.lrc;*.yaml;*.yml;*.lyricsfile.yaml|LRC files|*.lrc|Lyricsfile YAML|*.yaml;*.yml;*.lyricsfile.yaml",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        var languageDialog = new TranslationLanguageWindow { Owner = this };
        if (languageDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(languageDialog.Language))
        {
            return;
        }

        var loaded = await _viewModel.LoadTranslationFileAsync(dialog.FileName, languageDialog.Language);
        if (!loaded)
        {
            MessageBox.Show(
                this,
                _viewModel.TranslationStatusText,
                "Translation not loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RemoveTranslation_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RemoveTranslationAsync();
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import lyrics",
            Filter = "Lyrics files|*.lrc;*.yaml;*.yml;*.lyricsfile.yaml|LRC files|*.lrc|Lyricsfile YAML|*.yaml;*.yml;*.lyricsfile.yaml",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.LoadLocalLyricsFileAsync(dialog.FileName);
        }
    }
}
