using System.ComponentModel;
using System.Diagnostics;
using MessageBox = System.Windows.MessageBox;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Microsoft.Win32;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Desktop.Effects;
using SkyLyrics.Desktop.Services;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Infrastructure.Assets;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeService _windowModes;
    private readonly IAppPreferencesStore _preferences;
    private readonly SyncScheduler _scheduler;
    private readonly ISelectablePlaybackSessionSource _playbackSource;
    private readonly ILyricsLibrary _lyricsLibrary;
    private LibraryWindow? _libraryWindow;
    private VisualStudioWindow? _visualStudioWindow;
    private AssetLibraryWindow? _assetLibraryWindow;
    private bool _isFullscreen;
    private WindowState _restoreWindowState;
    private WindowStyle _restoreWindowStyle;
    private ResizeMode _restoreResizeMode;
    private readonly DispatcherTimer _geometrySaveTimer;
    private DispatcherTimer? _grainJitterTimer;
    private double _grainPhase;
    private CinematicQualityLevel _effectiveQualityLevel = CinematicQualityLevel.Balanced;
    private readonly Stopwatch _noteMotionClock = new();
    private bool _noteMotionSubscribed;
    private TimeSpan _previousNotePosition;
    private bool _hasPreviousNotePosition;
    private readonly SemaphoreSlim _geometrySaveGate = new(1, 1);
    private bool _closeSavePending;
    private bool _allowCloseAfterSave;

    private const double InitialWindowWidth = 1280d;
    private const double InitialWindowHeight = 800d;
    private const double MinimumWindowWidth = 980d;
    private const double MinimumWindowHeight = 610d;

    public MainWindow(
        ShellViewModel viewModel,
        WindowModeService windowModes,
        IAppPreferencesStore preferences,
        SyncScheduler scheduler,
        ISelectablePlaybackSessionSource playbackSource,
        ILyricsLibrary lyricsLibrary)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _windowModes = windowModes ?? throw new ArgumentNullException(nameof(windowModes));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _playbackSource = playbackSource ?? throw new ArgumentNullException(nameof(playbackSource));
        _lyricsLibrary = lyricsLibrary ?? throw new ArgumentNullException(nameof(lyricsLibrary));
        InitializeComponent();
        ApplyLibraryBackground(_preferences.Current.AssetLibrary.SelectedBackgroundId);
        _effectiveQualityLevel = ResolveQualityLevel(_preferences.Current.CinematicQualityTier);
        MusicWaveform.EffectiveQualityChanged += OnEffectiveQualityChanged;
        ApplyInitialWindowPlacement();
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _preferences.PreferencesChanged += OnPreferencesChanged;
        _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450d) };
        _geometrySaveTimer.Tick += GeometrySaveTimer_Tick;
        Closing += OnClosing;
        Loaded += OnLoaded;
        LocationChanged += (_, _) => ScheduleGeometrySave();
        SizeChanged += (_, _) =>
        {
            UpdateResponsiveLayout();
            UpdateLetterboxBars();
            ScheduleGeometrySave();
        };
        StateChanged += (_, _) => ScheduleGeometrySave();
        Closed += (_, _) =>
        {
            _geometrySaveTimer.Stop();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _preferences.PreferencesChanged -= OnPreferencesChanged;
            MusicWaveform.EffectiveQualityChanged -= OnEffectiveQualityChanged;
            StopGrainJitter();
            StopNoteMotion(resetTransforms: true);
            _windowModes.CloseAuxiliaryWindows();
            if (System.Windows.Application.Current is { Dispatcher.HasShutdownStarted: false })
                Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current?.Shutdown()));
        };
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        // Keep the close path alive until the latest normal geometry is safely persisted.
        if (_allowCloseAfterSave)
        {
            return;
        }

        e.Cancel = true;
        if (_closeSavePending)
        {
            return;
        }

        _closeSavePending = true;
        _geometrySaveTimer.Stop();
        _windowModes.CloseAuxiliaryWindows();

        try
        {
            await PersistWindowGeometryAsync();
        }
        finally
        {
            _allowCloseAfterSave = true;
            Close();
        }
    }

    public void RequestApplicationExit()
    {
        _windowModes.CloseAuxiliaryWindows();
        Close();
    }

    private void ApplyInitialWindowPlacement()
    {
        var workAreas = MonitorWorkAreaProvider.GetWorkAreas();
        var saved = _preferences.Current.MainWindowGeometry?.Normalize();
        WindowBounds? restoredBounds = null;
        WindowBounds? restoredWorkArea = null;

        if (saved is not null)
        {
            var candidate = saved.ToBounds();
            foreach (var area in workAreas)
            {
                if (!WindowPlacementCalculator.IsFullyVisibleOnAnyMonitor(candidate, [area]))
                {
                    continue;
                }

                restoredBounds = candidate;
                restoredWorkArea = area;
                break;
            }
        }

        var primaryWorkArea = MonitorWorkAreaProvider.GetPrimaryWorkArea();
        var selectedArea = restoredWorkArea ?? primaryWorkArea;
        var initialBounds = restoredBounds ??
            WindowPlacementCalculator.CenterAndClamp(
                new WindowBounds(0d, 0d, InitialWindowWidth, InitialWindowHeight),
                primaryWorkArea,
                MinimumWindowWidth,
                MinimumWindowHeight);

        MinWidth = Math.Min(MinimumWindowWidth, selectedArea.Width);
        MinHeight = Math.Min(MinimumWindowHeight, selectedArea.Height);
        if (restoredBounds is { } savedBounds)
        {
            MinWidth = Math.Min(MinWidth, savedBounds.Width);
            MinHeight = Math.Min(MinHeight, savedBounds.Height);
        }

        Width = initialBounds.Width;
        Height = initialBounds.Height;
        Left = initialBounds.Left;
        Top = initialBounds.Top;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout();
        UpdateLetterboxBars();
        PlayEntranceAnimation();
        ApplyAmbientMotionPreferences();
        ApplyCinematicEffects();
        AnimateArtwork();
    }

    private void ScheduleGeometrySave()
    {
        if (!IsLoaded || _isFullscreen || WindowState != WindowState.Normal)
        {
            return;
        }

        _geometrySaveTimer.Stop();
        _geometrySaveTimer.Start();
    }

    private async void GeometrySaveTimer_Tick(object? sender, EventArgs e)
    {
        _geometrySaveTimer.Stop();
        await PersistWindowGeometryAsync();
    }

    private async Task PersistWindowGeometryAsync()
    {
        var savedBounds = WindowState == WindowState.Normal
            ? new WindowBounds(Left, Top, Width, Height)
            : new WindowBounds(RestoreBounds.Left, RestoreBounds.Top, RestoreBounds.Width, RestoreBounds.Height);

        if (!savedBounds.IsValid)
        {
            return;
        }

        await _geometrySaveGate.WaitAsync();
        try
        {
            var geometry = new WindowGeometryPreference(
                savedBounds.Left,
                savedBounds.Top,
                savedBounds.Width,
                savedBounds.Height);

            await _preferences.SaveAsync(
                _preferences.Current with { MainWindowGeometry = geometry });
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Main window geometry could not be saved: {exception}");
        }
        finally
        {
            _geometrySaveGate.Release();
        }
    }

    private void UpdateLetterboxBars()
    {
        if (!IsInitialized || LyricStageBorder.ActualWidth <= 0d || LyricStageBorder.ActualHeight <= 0d)
        {
            return;
        }

        var bars = LetterboxCalculator.Calculate(
            LyricStageBorder.ActualWidth,
            LyricStageBorder.ActualHeight,
            _preferences.Current.LetterboxEnabled);

        LetterboxTopBar.Height = bars.Height;
        LetterboxBottomBar.Height = bars.Height;
        var visibility = bars.Height > 0d ? Visibility.Visible : Visibility.Collapsed;
        LetterboxTopBar.Visibility = visibility;
        LetterboxBottomBar.Visibility = visibility;
    }

    private void UpdateResponsiveLayout()
    {
        if (!IsInitialized || ActualWidth <= 0d) return;

        var width = ActualWidth;
        var compact = width < 1120d;
        var narrow = width < 1320d;

        RootGrid.Margin = new Thickness(compact ? 10d : narrow ? 14d : 20d);
        RootGrid.ColumnDefinitions[0].Width = new GridLength(compact ? 176d : 204d);
        RootGrid.ColumnDefinitions[2].Width = compact
            ? new GridLength(0d)
            : new GridLength(narrow ? 238d : 272d);
        RightSidebar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MainContentGrid.Margin = new Thickness(compact ? 10d : narrow ? 12d : 16d, 0d, compact ? 10d : 16d, 0d);

        ActiveLyricTextBlock.FontSize = compact ? 30d : narrow ? 33d : 36d;
        ActiveLyricTextBlock.LineHeight = compact ? 39d : narrow ? 43d : 46d;
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

    private void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_libraryWindow is { IsVisible: true })
        {
            _libraryWindow.Activate();
            return;
        }

        var window = new LibraryWindow(_viewModel, _lyricsLibrary) { Owner = this };
        _libraryWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_libraryWindow, window))
            {
                _libraryWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    private void OpenAssetLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_assetLibraryWindow is { IsVisible: true })
        {
            _assetLibraryWindow.Activate();
            return;
        }

        var window = new AssetLibraryWindow(_preferences) { Owner = this };
        _assetLibraryWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_assetLibraryWindow, window))
            {
                _assetLibraryWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    private void ApplyLibraryBackground(string assetId)
    {
        AmbientBackgroundImage.Source = null;
        AmbientBackgroundImage.Opacity = 0;

        if (string.IsNullOrWhiteSpace(assetId))
        {
            return;
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Library"));
        var relativeBase = assetId.Replace('/', Path.DirectorySeparatorChar);
        foreach (var extension in new[] { ".webp", ".png", ".jpg", ".jpeg" })
        {
            var path = Path.GetFullPath(Path.Combine(root, relativeBase + extension));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                continue;
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = _preferences.Current.AssetLibrary.QualityProfile switch
                {
                    "low" => 960,
                    "high" => 2400,
                    _ => 1600
                };
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                AmbientBackgroundImage.Source = bitmap;
                AmbientBackgroundImage.Opacity = _preferences.Current.AssetLibrary.QualityProfile switch
                {
                    "low" => 0.14,
                    "high" => 0.28,
                    _ => 0.21
                };
                return;
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidDataException or UriFormatException)
            {
                Trace.TraceWarning("SkyLyrics background asset could not be decoded: {0}", exception.Message);
                return;
            }
        }

        Trace.TraceWarning("Selected SkyLyrics background asset was not found: {0}", assetId);
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

    private void OnPreferencesChanged(AppPreferences preferences)
    {
        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _effectiveQualityLevel = ResolveQualityLevel(preferences.CinematicQualityTier);
                ApplyAmbientMotionPreferences();
                ApplyCinematicEffects();
                AnimateArtwork();
                UpdateLetterboxBars();
                ApplyLibraryBackground(preferences.AssetLibrary.SelectedBackgroundId);
                if (preferences.ReduceMotion || !SystemParameters.ClientAreaAnimation)
                {
                    ResetAnimationElement(ActiveLyricTextBlock);
                    ResetAnimationElement(PreviousLyricTextBlock);
                    ResetAnimationElement(UpcomingLyricTextBlock);
                    ResetAnimationElement(LyricsPanel);
                }
            }));
        }
    }

    private static CinematicQualityLevel ResolveQualityLevel(string quality) =>
        quality.ToLowerInvariant() switch
        {
            "cinematic" => CinematicQualityLevel.Cinematic,
            "lite" => CinematicQualityLevel.Lite,
            "reduced" => CinematicQualityLevel.Reduced,
            _ => CinematicQualityLevel.Balanced
        };

    private void OnEffectiveQualityChanged(CinematicQualityLevel level)
    {
        _effectiveQualityLevel = level;
        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyAmbientMotionPreferences();
                ApplyCinematicEffects();
            }));
        }
    }

    private void ApplyCinematicEffects()
    {
        var preferences = _preferences.Current;
        var active = (_effectiveQualityLevel is CinematicQualityLevel.Cinematic or CinematicQualityLevel.Balanced) &&
                     !preferences.ReduceMotion &&
                     SystemParameters.ClientAreaAnimation;

        BokehTexture.Opacity = _effectiveQualityLevel switch
        {
            CinematicQualityLevel.Cinematic => 0.15d,
            CinematicQualityLevel.Balanced => 0.08d,
            _ => 0d
        };
        LightLeakTexture.Opacity = _effectiveQualityLevel == CinematicQualityLevel.Cinematic ? 0.13d : 0d;
        GrainTextureLayer.Opacity = _effectiveQualityLevel switch
        {
            CinematicQualityLevel.Cinematic => 0.12d,
            CinematicQualityLevel.Balanced => 0.08d,
            CinematicQualityLevel.Lite => 0.04d,
            _ => 0d
        };
        VignetteTexture.Opacity = _effectiveQualityLevel == CinematicQualityLevel.Reduced ? 0.25d : 0.55d;

        if (active)
        {
            try
            {
                ArtworkBackdrop.Effect = new ChromaticAberrationShaderEffect { Amount = 0.18d };
                GrainTextureLayer.Effect = new GrainShaderEffect { Intensity = 0.018d, Phase = _grainPhase };
                StartGrainJitter();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"SkyLyrics shader effects are unavailable; vector textures remain active. {exception}");
                ArtworkBackdrop.Effect = null;
                GrainTextureLayer.Effect = null;
                StopGrainJitter();
            }
        }
        else
        {
            ArtworkBackdrop.Effect = null;
            GrainTextureLayer.Effect = null;
            StopGrainJitter();
        }
    }

    private void StartGrainJitter()
    {
        if (_grainJitterTimer is not null)
        {
            return;
        }

        _grainJitterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(83.333d)
        };
        _grainJitterTimer.Tick += (_, _) =>
        {
            _grainPhase += 0.61803398875d;
            if (_grainPhase >= 4096d)
            {
                _grainPhase = 0d;
            }

            if (GrainTextureLayer.Effect is GrainShaderEffect effect)
            {
                effect.Phase = _grainPhase;
            }
        };
        _grainJitterTimer.Start();
    }

    private void StopGrainJitter()
    {
        if (_grainJitterTimer is null)
        {
            return;
        }

        _grainJitterTimer.Stop();
        _grainJitterTimer = null;
        _grainPhase = 0d;
    }

    private void StartBokehMotion()
    {
        if (BokehTexture.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1d, 1.08d, TimeSpan.FromSeconds(11d))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1d, 1.08d, TimeSpan.FromSeconds(11d))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
    }

    private void StopBokehMotion()
    {
        if (BokehTexture.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1d;
        scale.ScaleY = 1d;
    }

    private void ApplyAmbientMotionPreferences()
    {
        var settings = _preferences.Current;
        var motionEnabled = SystemParameters.ClientAreaAnimation && !settings.ReduceMotion;
        var qualityEnabled = _effectiveQualityLevel != CinematicQualityLevel.Reduced;

        FloatingNoteOne.Visibility = qualityEnabled ? Visibility.Visible : Visibility.Collapsed;
        FloatingNoteTwo.Visibility = _effectiveQualityLevel is CinematicQualityLevel.Cinematic or CinematicQualityLevel.Balanced
            ? Visibility.Visible
            : Visibility.Collapsed;
        FloatingNoteThree.Visibility = _effectiveQualityLevel == CinematicQualityLevel.Cinematic
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!motionEnabled)
        {
            StopNoteMotion(resetTransforms: true);
            StopBokehMotion();
            return;
        }

        UpdateNoteMotionSubscription();

        if (_effectiveQualityLevel is CinematicQualityLevel.Cinematic or CinematicQualityLevel.Balanced)
        {
            StartBokehMotion();
        }
        else
        {
            StopBokehMotion();
        }
    }

    private void UpdateNoteMotionSubscription()
    {
        var shouldAnimate =
            IsLoaded &&
            _viewModel.ShowPlaybackVisual &&
            _viewModel.MusicVisualIsPlaying &&
            _viewModel.MusicVisualMotionEnabled &&
            SystemParameters.ClientAreaAnimation &&
            !_preferences.Current.ReduceMotion &&
            _effectiveQualityLevel != CinematicQualityLevel.Reduced;

        if (shouldAnimate && !_noteMotionSubscribed)
        {
            _noteMotionClock.Restart();
            _previousNotePosition = _viewModel.MusicVisualPosition;
            _hasPreviousNotePosition = true;
            CompositionTarget.Rendering += OnAmbientRendering;
            _noteMotionSubscribed = true;
        }
        else if (!shouldAnimate && _noteMotionSubscribed)
        {
            StopNoteMotion(resetTransforms: false);
        }
    }

    private void OnAmbientRendering(object? sender, EventArgs e)
    {
        var position = _viewModel.MusicVisualPosition;
        if (_hasPreviousNotePosition &&
            (position - _previousNotePosition).Duration() > TimeSpan.FromSeconds(1.5d))
        {
            _noteMotionClock.Restart();
        }

        _previousNotePosition = position;
        _hasPreviousNotePosition = true;
        var mediaSeconds = Math.Max(0d, position.TotalSeconds) + _noteMotionClock.Elapsed.TotalSeconds;

        SetNoteOffset(FloatingNoteOne, Math.Sin(mediaSeconds * 0.72d) * 15d);
        if (_effectiveQualityLevel is CinematicQualityLevel.Cinematic or CinematicQualityLevel.Balanced)
        {
            SetNoteOffset(FloatingNoteTwo, Math.Sin((mediaSeconds * 0.53d) + 1.7d) * 12d);
        }

        if (_effectiveQualityLevel == CinematicQualityLevel.Cinematic)
        {
            SetNoteOffset(FloatingNoteThree, Math.Sin((mediaSeconds * 0.41d) + 3.1d) * 10d);
        }
    }

    private static void SetNoteOffset(System.Windows.FrameworkElement note, double offset)
    {
        if (note.RenderTransform is TranslateTransform transform)
        {
            transform.Y = offset;
        }
    }

    private void StopNoteMotion(bool resetTransforms)
    {
        if (_noteMotionSubscribed)
        {
            CompositionTarget.Rendering -= OnAmbientRendering;
            _noteMotionSubscribed = false;
        }

        _noteMotionClock.Stop();
        if (resetTransforms)
        {
            SetNoteOffset(FloatingNoteOne, 0d);
            SetNoteOffset(FloatingNoteTwo, 0d);
            SetNoteOffset(FloatingNoteThree, 0d);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveLyricLineIdentity))
        {
            Dispatcher.BeginInvoke(new Action(AnimateLyricsTransition));
        }
        else if (e.PropertyName == nameof(ShellViewModel.AlbumArtwork))
        {
            Dispatcher.BeginInvoke(new Action(AnimateArtwork));
        }
        else if (e.PropertyName is nameof(ShellViewModel.MusicVisualPosition) or
                 nameof(ShellViewModel.MusicVisualIsPlaying) or
                 nameof(ShellViewModel.ShowPlaybackVisual) or
                 nameof(ShellViewModel.MusicVisualMotionEnabled))
        {
            Dispatcher.BeginInvoke(new Action(UpdateNoteMotionSubscription));
        }
    }

    private void AnimateLyricsTransition()
    {
        if (!SystemParameters.ClientAreaAnimation || _preferences.Current.ReduceMotion)
        {
            ResetAnimationElement(ActiveLyricTextBlock);
            ResetAnimationElement(PreviousLyricTextBlock);
            ResetAnimationElement(UpcomingLyricTextBlock);
            ResetAnimationElement(LyricsPanel);
            return;
        }

        // Horizontal scale settling supplies a restrained optical tracking-in.
        AnimateLyricsElement(ActiveLyricTextBlock, 0.08d, 24d, 0.96d, 360d, 1d, 1d);
        AnimateLyricsElement(PreviousLyricTextBlock, 0.76d, -14d, 1d, 280d, 0.25d, 0.92d);
        AnimateLyricsElement(UpcomingLyricTextBlock, 0.34d, 12d, 0.985d, 300d, 1d, 1d);
        AnimateLyricsElement(LyricsPanel, 0.35d, 10d, 0.99d, 380d, 1d, 1d);
    }

    private static void ResetAnimationElement(UIElement element)
    {
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = 1d;

        if (element is not FrameworkElement frameworkElement ||
            frameworkElement.RenderTransform is not TransformGroup group)
        {
            return;
        }

        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (translate is not null)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0d;
        }

        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
        if (scale is not null)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
        }
    }

    private static void AnimateLyricsElement(
        UIElement element,
        double fromOpacity,
        double fromY,
        double fromScale,
        double durationMilliseconds,
        double toOpacity,
        double toScale)
    {
        var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        element.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(fromOpacity, toOpacity, duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });

        if (element is not FrameworkElement frameworkElement ||
            frameworkElement.RenderTransform is not TransformGroup group)
        {
            return;
        }

        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (translate is not null)
        {
            translate.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(fromY, 0d, duration)
                {
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
                });
        }

        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
        if (scale is not null)
        {
            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(fromScale, toScale, duration)
                {
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
                });
            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(fromScale, 1d, duration)
                {
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
                });
        }
    }

    private void AnimateArtwork()
    {
        if (!SystemParameters.ClientAreaAnimation || _preferences.Current.ReduceMotion || !_viewModel.HasAlbumArtwork)
        {
            ArtworkBackdrop.BeginAnimation(OpacityProperty, null);
            ArtworkBackdrop.Opacity = _viewModel.HasAlbumArtwork ? 0.15d : 0d;
            if (ArtworkBackdrop.RenderTransform is ScaleTransform reducedScale)
            {
                reducedScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                reducedScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                reducedScale.ScaleX = 1d;
                reducedScale.ScaleY = 1d;
            }
            return;
        }

        ArtworkBackdrop.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0d, 0.15d, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });

        if (ArtworkBackdrop.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.035d, 1.085d, TimeSpan.FromSeconds(16d))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.035d, 1.085d, TimeSpan.FromSeconds(16d))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
        }
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

    private async void SearchLyricsCandidates_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SearchLyricsCandidatesAsync();
    }

    private async void SelectLyricsCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button &&
            button.Tag is LyricsSearchCandidate candidate)
        {
            await _viewModel.SelectLyricsCandidateAsync(candidate);
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
        if (languageDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(languageDialog.LanguageLabel))
        {
            return;
        }

        var loaded = await _viewModel.LoadTranslationFileAsync(dialog.FileName, languageDialog.LanguageLabel);
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
