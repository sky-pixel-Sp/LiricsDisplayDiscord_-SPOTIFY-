namespace SkyLyrics.Application.Settings;

public readonly record struct WindowBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Top) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0d &&
        Height > 0d &&
        double.IsFinite(Right) &&
        double.IsFinite(Bottom);
}

public static class WindowPlacementCalculator
{
    public static WindowBounds CenterAndClamp(
        WindowBounds requested,
        WindowBounds workArea,
        double minimumWidth,
        double minimumHeight)
    {
        Validate(requested, workArea, minimumWidth, minimumHeight);

        var centered = requested with
        {
            Left = workArea.Left + ((workArea.Width - requested.Width) / 2d),
            Top = workArea.Top + ((workArea.Height - requested.Height) / 2d)
        };

        return ClampToWorkArea(centered, workArea, minimumWidth, minimumHeight);
    }

    public static WindowBounds ClampToWorkArea(
        WindowBounds requested,
        WindowBounds workArea,
        double minimumWidth,
        double minimumHeight)
    {
        Validate(requested, workArea, minimumWidth, minimumHeight);

        // A display can be smaller than the normal minimum window size at high DPI.
        // In that case the work area itself becomes the effective minimum.
        var effectiveMinimumWidth = Math.Min(minimumWidth, workArea.Width);
        var effectiveMinimumHeight = Math.Min(minimumHeight, workArea.Height);

        var width = Math.Clamp(requested.Width, effectiveMinimumWidth, workArea.Width);
        var height = Math.Clamp(requested.Height, effectiveMinimumHeight, workArea.Height);
        var left = Math.Clamp(requested.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(requested.Top, workArea.Top, workArea.Bottom - height);

        return new WindowBounds(left, top, width, height);
    }

    public static bool IsFullyVisibleOnAnyMonitor(
        WindowBounds candidate,
        IEnumerable<WindowBounds> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        if (!candidate.IsValid)
        {
            return false;
        }

        foreach (var workArea in workAreas)
        {
            if (!workArea.IsValid)
            {
                continue;
            }

            if (candidate.Left >= workArea.Left &&
                candidate.Top >= workArea.Top &&
                candidate.Right <= workArea.Right &&
                candidate.Bottom <= workArea.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private static void Validate(
        WindowBounds requested,
        WindowBounds workArea,
        double minimumWidth,
        double minimumHeight)
    {
        if (!requested.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(requested), "Requested window bounds must be finite and positive.");
        }

        if (!workArea.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "The monitor work area must be finite and positive.");
        }

        if (!double.IsFinite(minimumWidth) || minimumWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        }

        if (!double.IsFinite(minimumHeight) || minimumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        }
    }
}

public sealed record WindowGeometryPreference(double Left, double Top, double Width, double Height)
{
    public WindowBounds ToBounds() => new(Left, Top, Width, Height);

    public WindowGeometryPreference? Normalize()
    {
        if (!ToBounds().IsValid ||
            Left is < -65536d or > 65536d ||
            Top is < -65536d or > 65536d ||
            Width is < 320d or > 16384d ||
            Height is < 240d or > 16384d)
        {
            return null;
        }

        return this;
    }
}
