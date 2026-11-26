namespace SkyLyrics.Core.Lyrics;

/// <summary>
/// Calculates a ScrollViewer pixel offset from a line's absolute content coordinate.
/// The input line coordinate is already relative to the full content surface; the
/// current VerticalOffset must not be added to it.
/// </summary>
public static class LyricsScrollTargetCalculator
{
    public static double Calculate(
        double lineCenterContentY,
        double viewportHeight,
        double scrollableHeight,
        double verticalAlignment = 0.48)
    {
        if (!double.IsFinite(lineCenterContentY))
            throw new ArgumentOutOfRangeException(nameof(lineCenterContentY));
        if (!double.IsFinite(viewportHeight) || viewportHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        if (!double.IsFinite(scrollableHeight) || scrollableHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(scrollableHeight));
        if (!double.IsFinite(verticalAlignment) || verticalAlignment < 0 || verticalAlignment > 1)
            throw new ArgumentOutOfRangeException(nameof(verticalAlignment));

        return Math.Clamp(
            lineCenterContentY - viewportHeight * verticalAlignment,
            0,
            scrollableHeight);
    }
}
