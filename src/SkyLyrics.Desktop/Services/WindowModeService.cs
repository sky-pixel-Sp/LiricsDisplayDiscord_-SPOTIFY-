using System.Diagnostics;
using System.Windows;
using SkyLyrics.Application.Settings;
using SkyLyrics.Desktop.ViewModels;

namespace SkyLyrics.Desktop.Services;

public sealed class WindowModeService : IDisposable
{
    private readonly ShellViewModel _viewModel;
    private readonly IAppPreferencesStore _preferences;
    private readonly SemaphoreSlim _gameModeSaveGate = new(1, 1);
    private LyricsFocusWindow? _focusWindow;
    private FloatingPlayerWindow? _miniWindow;
    private GameModeWindow? _gameModeWindow;
    private bool _disposed;

    public WindowModeService(ShellViewModel viewModel, IAppPreferencesStore preferences)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _preferences.PreferencesChanged += OnPreferencesChanged;
    }

    public bool IsMiniPlayerVisible => _miniWindow?.IsVisible == true;
    public bool IsFocusViewVisible => _focusWindow?.IsVisible == true;
    public bool IsGameModeVisible => _gameModeWindow?.IsVisible == true;

    public void ShowFocusView()
    {
        if (_focusWindow is { IsLoaded: true })
        {
            _focusWindow.Activate();
            return;
        }

        var window = new LyricsFocusWindow(_viewModel);
        _focusWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_focusWindow, window))
            {
                _focusWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    public void ToggleMiniPlayer()
    {
        if (_miniWindow is { IsVisible: true })
        {
            _miniWindow.Hide();
            return;
        }

        if (_miniWindow is { IsLoaded: true })
        {
            _miniWindow.Topmost = _preferences.Current.MiniPlayerAlwaysOnTop;
            _miniWindow.Show();
            _miniWindow.Activate();
            return;
        }

        var window = new FloatingPlayerWindow(_viewModel)
        {
            Left = SystemParameters.WorkArea.Right - 454,
            Top = SystemParameters.WorkArea.Bottom - 270,
            Topmost = _preferences.Current.MiniPlayerAlwaysOnTop
        };
        _miniWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_miniWindow, window))
            {
                _miniWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    public void ToggleGameMode()
    {
        if (_gameModeWindow is { IsVisible: true })
        {
            _gameModeWindow.Close();
            return;
        }

        ShowGameMode();
    }

    public void ShowGameMode()
    {
        if (_disposed) return;

        if (_gameModeWindow is { IsVisible: true })
        {
            _gameModeWindow.Activate();
            return;
        }

        var window = new GameModeWindow(
            _viewModel,
            _preferences.Current.GameMode,
            settings => _ = PersistGameModeSettingsAsync(settings));
        _gameModeWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_gameModeWindow, window))
            {
                _gameModeWindow = null;
            }
        };
        window.Show();
        window.Activate();
    }

    private void OnPreferencesChanged(AppPreferences preferences)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (_gameModeWindow is { } window && window.Settings != preferences.GameMode)
                window.ApplySettings(preferences.GameMode);
        }));
    }

    private async Task PersistGameModeSettingsAsync(GameModePreferences settings)
    {
        if (_disposed) return;
        try
        {
            await _gameModeSaveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _preferences.SaveAsync(_preferences.Current with { GameMode = settings }).ConfigureAwait(false);
            }
            finally
            {
                _gameModeSaveGate.Release();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Game Mode preferences could not be saved: {exception}");
        }
    }

    public void CloseAuxiliaryWindows()
    {
        _focusWindow?.Close();
        _miniWindow?.Close();
        _gameModeWindow?.Close();
        _focusWindow = null;
        _miniWindow = null;
        _gameModeWindow = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _preferences.PreferencesChanged -= OnPreferencesChanged;
        CloseAuxiliaryWindows();
    }
}
