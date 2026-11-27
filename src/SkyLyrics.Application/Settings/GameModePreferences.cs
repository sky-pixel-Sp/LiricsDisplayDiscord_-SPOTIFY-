namespace SkyLyrics.Application.Settings;

public sealed record GameModePreferences
{
    public double Left { get; init; } = -1d;
    public double Top { get; init; } = -1d;
    public double Width { get; init; } = 1000d;
    public double Height { get; init; } = 176d;
    public double Opacity { get; init; } = 0.96d;
    public bool AlwaysOnTop { get; init; } = true;
    public string BackgroundId { get; init; } = "aurora";
    public string TextEffectId { get; init; } = "mint-glow";
    public string AnimationId { get; init; } = "cinematic";

    public GameModePreferences Normalize()
    {
        static double Position(double value) =>
            double.IsFinite(value) && value is >= -32000d and <= 32000d ? value : -1d;

        static string Preset(string? value, string fallback, params string[] allowed)
        {
            foreach (var candidate in allowed)
            {
                if (string.Equals(value, candidate, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        return this with
        {
            Left = Position(Left),
            Top = Position(Top),
            Width = Math.Clamp(double.IsFinite(Width) ? Width : 1000d, 420d, 1920d),
            Height = Math.Clamp(double.IsFinite(Height) ? Height : 176d, 112d, 600d),
            Opacity = Math.Clamp(double.IsFinite(Opacity) ? Opacity : 0.96d, 0.55d, 1d),
            BackgroundId = Preset(BackgroundId, "aurora", "aurora", "ocean", "violet", "sunset", "graphite"),
            TextEffectId = Preset(TextEffectId, "mint-glow", "none", "mint-glow", "ice-glow", "soft-shadow"),
            AnimationId = Preset(AnimationId, "cinematic", "fade", "slide", "zoom", "spring", "snap")
        };
    }
}
