using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace SkyLyrics.Desktop.Services;

public sealed class SystemTrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action _showMain;
    private readonly Action _showFocus;
    private readonly Action _toggleMini;
    private readonly Action _toggleGameMode;
    private readonly Action _showSettings;
    private readonly Action _showDiagnostics;
    private readonly Action _exit;
    private bool _disposed;

    public SystemTrayService(Action showMain, Action showFocus, Action toggleMini, Action toggleGameMode,
        Action showSettings, Action showDiagnostics, Action exit)
    {
        _showMain = showMain ?? throw new ArgumentNullException(nameof(showMain));
        _showFocus = showFocus ?? throw new ArgumentNullException(nameof(showFocus));
        _toggleMini = toggleMini ?? throw new ArgumentNullException(nameof(toggleMini));
        _toggleGameMode = toggleGameMode ?? throw new ArgumentNullException(nameof(toggleGameMode));
        _showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));
        _showDiagnostics = showDiagnostics ?? throw new ArgumentNullException(nameof(showDiagnostics));
        _exit = exit ?? throw new ArgumentNullException(nameof(exit));

        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/SkyLyrics.Desktop;component/Assets/SkyLyrics.ico"))
            ?? throw new InvalidOperationException("The packaged SkyLyrics tray icon resource is missing.");
        using var iconStream = resource.Stream;
        using var icon = new Icon(iconStream);

        _notifyIcon = new NotifyIcon
        {
            Icon = (Icon)icon.Clone(),
            Text = "SkyLyrics Desktop",
            Visible = true,
            ContextMenuStrip = CreateMenu()
        };
        _notifyIcon.DoubleClick += OnDoubleClick;
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open SkyLyrics", null, (_, _) => Dispatch(_showMain));
        menu.Items.Add("Lyrics Focus  ·  Ctrl+Alt+K", null, (_, _) => Dispatch(_showFocus));
        menu.Items.Add("Toggle Mini Player  ·  Ctrl+Alt+M", null, (_, _) => Dispatch(_toggleMini));
        menu.Items.Add("Toggle Game Mode  ·  Ctrl+Alt+G", null, (_, _) => Dispatch(_toggleGameMode));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => Dispatch(_showSettings));
        menu.Items.Add("Diagnostics", null, (_, _) => Dispatch(_showDiagnostics));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit SkyLyrics", null, (_, _) => Dispatch(_exit));
        return menu;
    }

    private void OnDoubleClick(object? sender, EventArgs e) => Dispatch(_showMain);

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.DoubleClick -= OnDoubleClick;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}