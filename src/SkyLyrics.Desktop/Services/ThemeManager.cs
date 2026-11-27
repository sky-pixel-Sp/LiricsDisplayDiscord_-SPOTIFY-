using System.Windows;
using System.Windows.Media;
using SkyLyrics.Application.Customization;

namespace SkyLyrics.Desktop.Services;

public static class ThemeManager
{
    private static readonly Uri DarkThemeUri =
        new("/SkyLyrics.Desktop;component/Themes/Dark.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri =
        new("/SkyLyrics.Desktop;component/Themes/Light.xaml", UriKind.Relative);

    public static void Apply(string? theme)
    {
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var source = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? LightThemeUri
            : DarkThemeUri;
        var dictionaries = application.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) == true);

        if (current?.Source != source)
        {
            var next = new ResourceDictionary { Source = source };
            if (current is null)
            {
                dictionaries.Add(next);
            }
            else
            {
                dictionaries[dictionaries.IndexOf(current)] = next;
            }
        }
    }

    public static void ApplyProfile(ThemeProfile? profile)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var selected = (profile ?? CustomizationPreferences.Sage).Normalize();
        var background = Parse(selected.BackgroundColor);
        var surface = Parse(selected.SurfaceColor);
        var accent = Parse(selected.AccentColor);
        var primary = Parse(selected.PrimaryTextColor);
        var secondary = Parse(selected.SecondaryTextColor);

        resources["WindowBackground"] = Brush(background);
        resources["PanelBackground"] = Brush(surface);
        resources["PanelBackgroundRaised"] = Brush(Mix(surface, primary, 0.055d));
        resources["PanelHoverBrush"] = Brush(Mix(surface, accent, 0.12d));
        resources["PanelBorder"] = Brush(Mix(surface, primary, 0.16d));
        resources["AccentBrush"] = Brush(accent);
        resources["AccentSoftBrush"] = Brush(Mix(background, accent, 0.18d));
        resources["PrimaryText"] = Brush(primary);
        resources["SecondaryText"] = Brush(secondary);
        resources["MutedText"] = Brush(Mix(secondary, background, 0.28d));
        resources["LyricsFontFamily"] = new FontFamily(selected.FontFamily);
        resources["LyricsActiveFontSize"] = 66d * selected.FontScale;
        resources["LyricsSecondaryFontSize"] = 24d * selected.FontScale;
        resources["LyricsUpcomingFontSize"] = 22d * selected.FontScale;
        resources["LyricsWordFontSize"] = 19d * selected.FontScale;
        resources["LyricsMiniFontSize"] = 17d * selected.FontScale;
        resources["LyricsTextAlignment"] = selected.LyricAlignment switch
        {
            "Left" => TextAlignment.Left,
            "Right" => TextAlignment.Right,
            _ => TextAlignment.Center
        };
        resources["LyricsArtworkOpacity"] = selected.ArtworkTreatment switch
        {
            "Cover" => 0.24d,
            "Minimal" => 0.04d,
            "Hidden" => 0d,
            _ => 0.10d
        };
        resources["LyricsArtworkVisibility"] = selected.ArtworkTreatment == "Hidden"
            ? Visibility.Collapsed
            : Visibility.Visible;
        resources["LyricsAnimationDurationMilliseconds"] = selected.AnimationPreset switch
        {
            "Static" => 0,
            "Subtle" => 180,
            "Pulse" => 260,
            _ => 420
        };
    }

    private static System.Windows.Media.Color Parse(string value) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

    private static SolidColorBrush Brush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color Mix(
        System.Windows.Media.Color first,
        System.Windows.Media.Color second,
        double amount)
    {
        var t = Math.Clamp(amount, 0d, 1d);
        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Round(first.R + ((second.R - first.R) * t)),
            (byte)Math.Round(first.G + ((second.G - first.G) * t)),
            (byte)Math.Round(first.B + ((second.B - first.B) * t)));
    }
}
