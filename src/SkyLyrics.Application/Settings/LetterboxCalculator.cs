namespace SkyLyrics.Application.Settings;

public readonly record struct LetterboxBars(double Height);

public static class LetterboxCalculator
{
    public const double CinematicAspectRatio = 2.39d;

    public static LetterboxBars Calculate(
        double width,
        double height,
        bool enabled,
        double aspectRatio = CinematicAspectRatio)
    {
        if (!enabled ||
            !double.IsFinite(width) ||
            !double.IsFinite(height) ||
            !double.IsFinite(aspectRatio) ||
            width <= 0d ||
            height <= 0d ||
            aspectRatio <= 0d)
        {
            return new LetterboxBars(0d);
        }

        var desiredContentHeight = width / aspectRatio;
        if (desiredContentHeight >= height)
        {
            return new LetterboxBars(0d);
        }

        return new LetterboxBars(Math.Max(0d, (height - desiredContentHeight) / 2d));
    }
}
