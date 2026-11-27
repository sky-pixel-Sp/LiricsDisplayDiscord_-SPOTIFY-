using System.Windows;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Application.Settings;

namespace SkyLyrics.Desktop.Services;

public sealed class WindowModeService
{
    private readonly ShellViewModel _viewModel;
    private readonly IAppPreferencesStore _preferences;
    private LyricsFocusWindow? _focusWindow;
    private FloatingPlayerWindow? _miniWindow;

    public WindowModeService(ShellViewModel viewModel, IAppPreferencesStore preferences)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public bool IsMiniPlayerVisible => _miniWindow?.IsVisible == true;
    public bool IsFocusViewVisible => _focusWindow?.IsVisible == true;

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

    public void CloseAuxiliaryWindows()
    {
        _focusWindow?.Close();
        _miniWindow?.Close();
        _focusWindow = null;
        _miniWindow = null;
    }
}
