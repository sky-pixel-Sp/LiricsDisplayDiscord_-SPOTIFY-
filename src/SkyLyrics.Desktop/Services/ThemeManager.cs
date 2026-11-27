using System.Windows;

namespace SkyLyrics.Desktop.Services;

public static class ThemeManager
{
    private static readonly Uri DarkThemeUri =
        new("/SkyLyrics.Desktop;component/Themes/Dark.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri =
        new("/SkyLyrics.Desktop;component/Themes/Light.xaml", UriKind.Relative);

    public static void Apply(string? theme)
    {
        var application = Application.Current;
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

        if (current?.Source == source)
        {
            return;
        }

        var next = new ResourceDictionary { Source = source };
        if (current is null)
        {
            dictionaries.Add(next);
        }
        else
        {
            var index = dictionaries.IndexOf(current);
            dictionaries[index] = next;
        }
    }
}
