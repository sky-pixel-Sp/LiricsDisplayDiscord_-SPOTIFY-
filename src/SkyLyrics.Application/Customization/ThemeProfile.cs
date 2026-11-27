using System.Text.RegularExpressions;

namespace SkyLyrics.Application.Customization;

public sealed record ThemeProfile
{
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{2,39}$", RegexOptions.CultureInvariant);

    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "custom-theme";
    public string Name { get; init; } = "Custom Theme";
    public string AccentColor { get; init; } = "#83E1D2";
    public string BackgroundColor { get; init; } = "#111817";
    public string SurfaceColor { get; init; } = "#1A2421";
    public string PrimaryTextColor { get; init; } = "#F2F6F3";
    public string SecondaryTextColor { get; init; } = "#AAB8B1";
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontScale { get; init; } = 1d;
    public string LyricAlignment { get; init; } = "Center";
    public string ArtworkTreatment { get; init; } = "Ambient";
    public string AnimationPreset { get; init; } = "Cinematic";

    public ThemeProfile Normalize()
    {
        var fallback = CustomizationPreferences.Sage;
        return this with
        {
            SchemaVersion = 1,
            Id = IsValidId(Id) ? Id : fallback.Id,
            Name = NormalizeText(Name, 40, fallback.Name),
            AccentColor = NormalizeColor(AccentColor, fallback.AccentColor),
            BackgroundColor = NormalizeColor(BackgroundColor, fallback.BackgroundColor),
            SurfaceColor = NormalizeColor(SurfaceColor, fallback.SurfaceColor),
            PrimaryTextColor = NormalizeColor(PrimaryTextColor, fallback.PrimaryTextColor),
            SecondaryTextColor = NormalizeColor(SecondaryTextColor, fallback.SecondaryTextColor),
            FontFamily = NormalizeText(FontFamily, 64, "Segoe UI"),
            FontScale = Math.Clamp(double.IsFinite(FontScale) ? FontScale : 1d, 0.8d, 1.4d),
            LyricAlignment = Allowed(LyricAlignment, "Center", "Left", "Right"),
            ArtworkTreatment = Allowed(ArtworkTreatment, "Ambient", "Cover", "Minimal", "Hidden"),
            AnimationPreset = Allowed(AnimationPreset, "Cinematic", "Subtle", "Static", "Pulse")
        };
    }

    public static bool IsValidId(string? value) =>
        value is not null && IdPattern.IsMatch(value);

    public static bool IsValidColor(string? value)
    {
        if (value is null || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeColor(string? value, string fallback) =>
        IsValidColor(value) ? value!.ToUpperInvariant() : fallback;

    private static string NormalizeText(string? value, int maximumLength, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? fallback
            : normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private static string Allowed(string? value, string fallback, params string[] allowed) =>
        allowed.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            ?? fallback;
}
