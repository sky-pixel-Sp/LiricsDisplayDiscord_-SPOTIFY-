using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Desktop.Services;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Synchronization;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeService _windowModes;
    private readonly IAppPreferencesStore _preferences;
    private readonly SyncScheduler _scheduler;
    private bool _allowClose;

    public MainWindow(ShellViewModel viewModel, WindowModeService windowModes, IAppPreferencesStore preferences, SyncScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _windowModes = windowModes ?? throw new ArgumentNullException(nameof(windowModes));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _windowModes.CloseAuxiliaryWindows();
        };
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
    }

    public void RequestApplicationExit()
    {
        _allowClose = true;
        Close();
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
        new SettingsWindow(_preferences, _scheduler) { Owner = this }.ShowDialog();
    }

    public void ShowDiagnostics()
    {
        ShowFromTray();
        new DiagnosticsWindow(_viewModel) { Owner = this }.Show();
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
            if (ActiveLyricTextBlock.RenderTransform is TranslateTransform reducedMotionTransform)
            {
                reducedMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
                reducedMotionTransform.Y = 0d;
            }

            return;
        }

        ActiveLyricTextBlock.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.42d, 1d, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        if (ActiveLyricTextBlock.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(11d, 0d, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
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

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
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
