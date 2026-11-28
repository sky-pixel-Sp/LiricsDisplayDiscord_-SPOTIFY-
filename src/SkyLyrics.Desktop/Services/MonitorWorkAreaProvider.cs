using System.Runtime.InteropServices;
using SkyLyrics.Application.Settings;
using FormsScreen = System.Windows.Forms.Screen;

namespace SkyLyrics.Desktop.Services;

public static class MonitorWorkAreaProvider
{
    private const uint MonitorDefaultToNearest = 2;

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
        var scaleFactor = GetMonitorScaleFactor(monitor);

        return WindowPlacementCalculator.ConvertPhysicalWorkAreaToLogical(
            new WindowBounds(workArea.Left, workArea.Top, workArea.Width, workArea.Height),
            scaleFactor);
    }

    private static double GetMonitorScaleFactor(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            return 1d;
        }

        try
        {
            var result = GetScaleFactorForMonitor(monitor, out var scalePercent);
            if (result == 0 && scalePercent > 0)
            {
                return scalePercent / 100d;
            }
        }
        catch (DllNotFoundException)
        {
            // The system API is optional on older Windows builds; retain pixel coordinates as a safe fallback.
        }
        catch (EntryPointNotFoundException)
        {
            // The system API is optional on older Windows builds; retain pixel coordinates as a safe fallback.
        }

        return 1d;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scalePercent);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }
}
