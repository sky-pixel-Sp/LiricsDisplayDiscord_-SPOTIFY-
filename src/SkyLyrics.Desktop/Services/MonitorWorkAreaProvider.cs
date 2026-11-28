using System.Runtime.InteropServices;
using SkyLyrics.Application.Settings;
using FormsScreen = System.Windows.Forms.Screen;

namespace SkyLyrics.Desktop.Services;

public static class MonitorWorkAreaProvider
{
    private const uint MonitorDefaultToNearest = 2;
    private const double DipsPerInch = 96d;

    public static IReadOnlyList<WindowBounds> GetWorkAreas()
    {
        var workAreas = new List<WindowBounds>();

        foreach (var screen in FormsScreen.AllScreens)
        {
            var area = screen.Primary
                ? FromWpfWorkArea(System.Windows.SystemParameters.WorkArea)
                : FromScreenWorkArea(screen);

            if (area.IsValid)
            {
                workAreas.Add(area);
            }
        }

        if (workAreas.Count == 0)
        {
            var fallback = GetPrimaryWorkArea();
            if (fallback.IsValid)
            {
                workAreas.Add(fallback);
            }
        }

        return workAreas;
    }

    public static WindowBounds GetPrimaryWorkArea()
    {
        var workArea = FromWpfWorkArea(System.Windows.SystemParameters.WorkArea);
        if (workArea.IsValid)
        {
            return workArea;
        }

        var primaryScreen = FormsScreen.AllScreens.FirstOrDefault(static screen => screen.Primary)
            ?? FormsScreen.PrimaryScreen;

        return primaryScreen is null
            ? new WindowBounds(0d, 0d, 1280d, 800d)
            : FromScreenWorkArea(primaryScreen);
    }

    private static WindowBounds FromWpfWorkArea(System.Windows.Rect area) =>
        new(area.Left, area.Top, area.Width, area.Height);

    private static WindowBounds FromScreenWorkArea(FormsScreen screen)
    {
        var workArea = screen.WorkingArea;
        var midpoint = new NativePoint(
            screen.Bounds.Left + (screen.Bounds.Width / 2),
            screen.Bounds.Top + (screen.Bounds.Height / 2));

        var monitor = MonitorFromPoint(midpoint, MonitorDefaultToNearest);
        var (scaleX, scaleY) = GetMonitorScale(monitor);

        return new WindowBounds(
            workArea.Left / scaleX,
            workArea.Top / scaleY,
            workArea.Width / scaleX,
            workArea.Height / scaleY);
    }

    private static (double ScaleX, double ScaleY) GetMonitorScale(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            return (1d, 1d);
        }

        try
        {
            var result = GetDpiForMonitor(
                monitor,
                MonitorDpiType.Effective,
                out var dpiX,
                out var dpiY);

            if (result == 0 && dpiX > 0 && dpiY > 0)
            {
                return (dpiX / DipsPerInch, dpiY / DipsPerInch);
            }
        }
        catch (DllNotFoundException)
        {
            // Older Windows images can lack Shcore; pixel coordinates remain a safe fallback.
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows images can lack GetDpiForMonitor.
        }

        return (1d, 1d);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    private enum MonitorDpiType
    {
        Effective = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }
}
