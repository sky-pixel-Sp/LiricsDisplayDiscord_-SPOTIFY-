using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SkyLyrics.Desktop.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int FocusHotkeyId = 0x534B01;
    private const int MiniHotkeyId = 0x534B02;
    private const int ShowHotkeyId = 0x534B03;
    private const int GameModeHotkeyId = 0x534B04;

    private readonly Window _window;
    private readonly WindowModeService _windowModes;
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _disposed;

    public GlobalHotkeyService(Window window, WindowModeService windowModes)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _windowModes = windowModes ?? throw new ArgumentNullException(nameof(windowModes));
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnWindowClosed;
}

    public event EventHandler<string>? RegistrationChanged;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowHook);

        Register(FocusHotkeyId, ModControl | ModAlt | ModNoRepeat, 0x4B, "Ctrl+Alt+K · Focus view");
        Register(MiniHotkeyId, ModControl | ModAlt | ModNoRepeat, 0x4D, "Ctrl+Alt+M · Mini player");
        Register(ShowHotkeyId, ModControl | ModAlt | ModNoRepeat, 0x53, "Ctrl+Alt+S · Show main window");
        Register(GameModeHotkeyId, ModControl | ModAlt | ModNoRepeat, 0x47, "Ctrl+Alt+G · Game Mode");
    }

    private void Register(int id, uint modifiers, uint key, string label)
    {
        var registered = RegisterHotKey(_handle, id, modifiers, key);
        RegistrationChanged?.Invoke(this, registered
            ? $"{label} registered."
            : $"{label} is unavailable because another application uses it.");
    }

    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case FocusHotkeyId:
                _windowModes.ShowFocusView();
                handled = true;
                break;
            case MiniHotkeyId:
                _windowModes.ToggleMiniPlayer();
                handled = true;
                break;
            case ShowHotkeyId:
                ShowMainWindow();
                handled = true;
                break;
            case GameModeHotkeyId:
                _windowModes.ToggleGameMode();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void ShowMainWindow()
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.ShowInTaskbar = true;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    private void OnWindowClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Closed -= OnWindowClosed;

        if (_source is not null)
        {
            _source.RemoveHook(WindowHook);
            _source = null;
        }

        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, FocusHotkeyId);
            UnregisterHotKey(_handle, MiniHotkeyId);
            UnregisterHotKey(_handle, ShowHotkeyId);
            UnregisterHotKey(_handle, GameModeHotkeyId);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
