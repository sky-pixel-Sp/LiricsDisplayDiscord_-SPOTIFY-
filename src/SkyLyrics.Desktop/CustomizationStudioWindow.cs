using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SkyLyrics.Application.Customization;
using SkyLyrics.Application.Settings;
using SkyLyrics.Desktop.Services;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using Panel = System.Windows.Controls.Panel;
using Slider = System.Windows.Controls.Slider;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace SkyLyrics.Desktop;

public sealed class CustomizationStudioWindow : Window
{
    private const int MaximumImportBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> BuiltInIds = new(StringComparer.OrdinalIgnoreCase)
        { "sage", "midnight", "paper" };

    private readonly IAppPreferencesStore _preferences;
    private readonly ObservableCollection<ThemeProfile> _profiles;
    private readonly ThemeProfile _originalActive;
    private readonly ListBox _profileList = new();
    private readonly TextBox _name = new();
    private readonly TextBox _accent = new();
    private readonly TextBox _background = new();
    private readonly TextBox _surface = new();
    private readonly TextBox _primary = new();
    private readonly TextBox _secondary = new();
    private readonly ComboBox _font = new();
    private readonly ComboBox _alignment = new();
    private readonly ComboBox _artwork = new();
    private readonly ComboBox _motion = new();
    private readonly Slider _fontScale = new();
    private readonly TextBlock _status = new();
    private bool _applied;
    private bool _loading;

    public CustomizationStudioWindow(IAppPreferencesStore preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        var customization = preferences.Current.Customization.Normalize();
        _profiles = new ObservableCollection<ThemeProfile>(customization.Profiles);
        _originalActive = _profiles.First(profile =>
            StringComparer.OrdinalIgnoreCase.Equals(profile.Id, customization.ActiveProfileId));

        Title = "SkyLyrics · Customization Studio";
        Width = 1120;
        Height = 820;
        MinWidth = 900;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0D10");
        Foreground = Brush("#F4F5F2");
        FontFamily = new FontFamily("Segoe UI Variable Display");
        Closed += (_, _) =>
        {
            if (!_applied) ThemeManager.ApplyProfile(_originalActive);
        };

        Content = BuildInterface();
        _profileList.ItemsSource = _profiles;
        _profileList.DisplayMemberPath = nameof(ThemeProfile.Name);
        _profileList.SelectionChanged += (_, _) => LoadSelected();
        _profileList.SelectedItem = _profiles.First(profile =>
            StringComparer.OrdinalIgnoreCase.Equals(profile.Id, customization.ActiveProfileId));
    }

    private UIElement BuildInterface()
    {
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(new TextBlock
        {
            Text = "APPEARANCE LAB",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        header.Children.Add(new TextBlock
        {
            Text = "Build a visual identity for SkyLyrics",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 0, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Edit, preview and save local theme profiles. Nothing is persisted until Apply.",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = new Grid { Margin = new Thickness(0, 0, 18, 0) };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Children.Add(Section("THEME PROFILES"));
        _profileList.Background = Brush("#12161B");
        _profileList.Foreground = Brush("#F4F5F2");
        _profileList.BorderBrush = Brush("#28323A");
        _profileList.Padding = new Thickness(6);
        Grid.SetRow(_profileList, 1);
        sidebar.Children.Add(_profileList);

        var profileActions = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        profileActions.ColumnDefinitions.Add(new ColumnDefinition());
        profileActions.ColumnDefinitions.Add(new ColumnDefinition());
        profileActions.RowDefinitions.Add(new RowDefinition());
        profileActions.RowDefinitions.Add(new RowDefinition());
        AddAction(profileActions, "DUPLICATE", 0, 0, Duplicate_Click);
        AddAction(profileActions, "DELETE", 0, 1, Delete_Click);
        AddAction(profileActions, "IMPORT JSON", 1, 0, Import_Click);
        AddAction(profileActions, "EXPORT JSON", 1, 1, Export_Click);
        Grid.SetRow(profileActions, 2);
        sidebar.Children.Add(profileActions);
        Grid.SetColumn(sidebar, 0);
        Grid.SetRow(body, 1);
        body.Children.Add(sidebar);

        var editor = new StackPanel();
        editor.Children.Add(Section("PROFILE"));
        editor.Children.Add(Field("Profile name", _name));
        editor.Children.Add(Section("COLOR TOKENS"));
        editor.Children.Add(Field("Accent · #RRGGBB", ColorInput(_accent)));
        editor.Children.Add(Field("Background · #RRGGBB", ColorInput(_background)));
        editor.Children.Add(Field("Surface · #RRGGBB", ColorInput(_surface)));
        editor.Children.Add(Field("Primary text · #RRGGBB", ColorInput(_primary)));
        editor.Children.Add(Field("Secondary text · #RRGGBB", ColorInput(_secondary)));
        editor.Children.Add(Section("TYPOGRAPHY & MOTION"));

        _font.IsEditable = true;
        _font.IsTextSearchEnabled = true;
        _font.MinHeight = 34;
        _font.ItemsSource = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(font => font, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        editor.Children.Add(Field("Installed Windows font", _font));

        _fontScale.Minimum = 0.8;
        _fontScale.Maximum = 1.4;
        _fontScale.TickFrequency = 0.05;
        _fontScale.IsSnapToTickEnabled = false;
        editor.Children.Add(Field("Font scale · 0.8× to 1.4×", _fontScale));

        _alignment.ItemsSource = new[] { "Center", "Left", "Right" };
        _artwork.ItemsSource = new[] { "Ambient", "Cover", "Minimal", "Hidden" };
        _motion.ItemsSource = new[] { "Cinematic", "Subtle", "Static", "Pulse" };
        editor.Children.Add(Field("Lyric alignment", _alignment));
        editor.Children.Add(Field("Artwork treatment", _artwork));
        editor.Children.Add(Field("Motion preset", _motion));
        editor.Children.Add(new TextBlock
        {
            Text = "JSON import accepts schema version 1 only. Theme files contain style data, never executable code. Fonts must already be installed on this PC.",
            FontSize = 10,
            Foreground = Brush("#A8B1B6"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        });

        var scroll = new ScrollViewer
        {
            Content = editor,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumn(scroll, 1);
        body.Children.Add(scroll);
        root.Children.Add(body);

        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Text = "Select a profile. Duplicate built-in profiles before editing them.";
        _status.FontSize = 10;
        _status.Foreground = Brush("#A8B1B6");
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        AddFooterButton(buttons, "CANCEL", (_, _) => Close());
        AddFooterButton(buttons, "PREVIEW", Preview_Click);
        AddFooterButton(buttons, "APPLY PROFILE", Apply_Click, true);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private void LoadSelected()
    {
        if (_profileList.SelectedItem is not ThemeProfile profile) return;
        _loading = true;
        _name.Text = profile.Name;
        _accent.Text = profile.AccentColor;
        _background.Text = profile.BackgroundColor;
        _surface.Text = profile.SurfaceColor;
        _primary.Text = profile.PrimaryTextColor;
        _secondary.Text = profile.SecondaryTextColor;
        _font.Text = profile.FontFamily;
        _fontScale.Value = profile.FontScale;
        _alignment.SelectedItem = profile.LyricAlignment;
        _artwork.SelectedItem = profile.ArtworkTreatment;
        _motion.SelectedItem = profile.AnimationPreset;
        _loading = false;
        _status.Text = BuiltInIds.Contains(profile.Id)
            ? "Built-in profile: duplicate it to create an editable custom profile."
            : "Custom profile selected. Apply to persist changes.";
    }

    private ThemeProfile? ReadEdited()
    {
        if (_loading || _profileList.SelectedItem is not ThemeProfile current)
        {
            _status.Text = "Select a profile before editing.";
            return null;
        }

        var colors = new[] { _accent.Text.Trim(), _background.Text.Trim(), _surface.Text.Trim(),
            _primary.Text.Trim(), _secondary.Text.Trim() };
        if (colors.Any(color => !ThemeProfile.IsValidColor(color)))
        {
            _status.Text = "All color tokens must use #RRGGBB format.";
            return null;
        }

        var name = _name.Text.Trim();
        if (name.Length is < 1 or > 40)
        {
            _status.Text = "Profile name must contain 1–40 characters.";
            return null;
        }

        var fontName = _font.Text.Trim();
        if (!Fonts.SystemFontFamilies.Any(font =>
                StringComparer.OrdinalIgnoreCase.Equals(font.Source, fontName)))
        {
            _status.Text = "Select a font installed on this Windows device.";
            return null;
        }

        return (current with
        {
            Name = name,
            AccentColor = colors[0],
            BackgroundColor = colors[1],
            SurfaceColor = colors[2],
            PrimaryTextColor = colors[3],
            SecondaryTextColor = colors[4],
            FontFamily = fontName,
            FontScale = _fontScale.Value,
            LyricAlignment = _alignment.SelectedItem as string ?? "Center",
            ArtworkTreatment = _artwork.SelectedItem as string ?? "Ambient",
            AnimationPreset = _motion.SelectedItem as string ?? "Cinematic"
        }).Normalize();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var profile = ReadEdited();
        if (profile is null) return;
        ThemeManager.ApplyProfile(profile);
        _status.Text = $"Previewing {profile.Name}. Press Apply Profile to save it.";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var profile = ReadEdited();
        if (profile is null) return;

        var updated = _profiles.Select(existing =>
            StringComparer.OrdinalIgnoreCase.Equals(existing.Id, profile.Id) ? profile : existing).ToList();
        var customization = (_preferences.Current.Customization with
        {
            ActiveProfileId = profile.Id,
            Profiles = updated
        }).Normalize();

        try
        {
            await _preferences.SaveAsync(_preferences.Current with { Customization = customization });
            ThemeManager.ApplyProfile(customization.Profiles.First(item =>
                StringComparer.OrdinalIgnoreCase.Equals(item.Id, customization.ActiveProfileId)));
            _applied = true;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not save theme profile: {exception.Message}";
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_profiles.Count >= CustomizationPreferences.MaximumProfiles)
        {
            _status.Text = $"Maximum profile count is {CustomizationPreferences.MaximumProfiles}.";
            return;
        }

        var source = ReadEdited();
        if (source is null) return;
        var slug = new string(source.Name.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (slug.Length < 3) slug = "custom";
        if (slug.Length > 28) slug = slug[..28].TrimEnd('-');
        var copy = source with { Id = $"{slug}-{Guid.NewGuid():N}"[..(slug.Length + 7)], Name = $"{source.Name} Copy" };
        _profiles.Add(copy);
        _profileList.SelectedItem = copy;
        _status.Text = "Duplicate created. Apply Profile to save it.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_profileList.SelectedItem is not ThemeProfile selected) return;
        if (BuiltInIds.Contains(selected.Id))
        {
            _status.Text = "Built-in profiles are protected. Duplicate one to customize it.";
            return;
        }

        if (MessageBox.Show(this, $"Delete '{selected.Name}' from pending changes?",
                "Delete custom profile", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _profiles.Remove(selected);
        _profileList.SelectedItem = _profiles.FirstOrDefault(profile =>
            StringComparer.OrdinalIgnoreCase.Equals(profile.Id, _preferences.Current.Customization.ActiveProfileId))
            ?? _profiles.FirstOrDefault();
        _status.Text = "Profile removed from pending changes. Apply another profile to save.";
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import SkyLyrics theme profile",
            Filter = "SkyLyrics theme profile (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var file = new FileInfo(dialog.FileName);
            if (!file.Exists || file.Length is <= 0 or > MaximumImportBytes)
                throw new InvalidDataException("Theme JSON must be between 1 byte and 64 KiB.");

            var json = await File.ReadAllTextAsync(dialog.FileName);
            var profile = JsonSerializer.Deserialize<ThemeProfile>(json)
                ?? throw new InvalidDataException("Theme JSON is empty.");
            if (profile.SchemaVersion != 1 || !ThemeProfile.IsValidId(profile.Id) ||
                !ThemeProfile.IsValidColor(profile.AccentColor) ||
                !ThemeProfile.IsValidColor(profile.BackgroundColor) ||
                !ThemeProfile.IsValidColor(profile.SurfaceColor) ||
                !ThemeProfile.IsValidColor(profile.PrimaryTextColor) ||
                !ThemeProfile.IsValidColor(profile.SecondaryTextColor))
                throw new InvalidDataException("Unsupported schema or invalid theme token.");

            if (_profiles.Count >= CustomizationPreferences.MaximumProfiles)
                throw new InvalidDataException($"Maximum profile count is {CustomizationPreferences.MaximumProfiles}.");

            if (_profiles.Any(existing => StringComparer.OrdinalIgnoreCase.Equals(existing.Id, profile.Id)))
            {
                var slug = profile.Id.Length > 28 ? profile.Id[..28] : profile.Id;
                profile = profile with { Id = $"{slug}-{Guid.NewGuid():N}"[..(slug.Length + 7)] };
            }

            var normalized = profile.Normalize();
            if (!Fonts.SystemFontFamilies.Any(font =>
                    StringComparer.OrdinalIgnoreCase.Equals(font.Source, normalized.FontFamily)))
                throw new InvalidDataException("The imported font is not installed on this device.");

            _profiles.Add(normalized);
            _profileList.SelectedItem = normalized;
            _status.Text = "Theme imported to the preview list. Apply to save it locally.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _status.Text = $"Theme import failed: {exception.Message}";
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var profile = ReadEdited();
        if (profile is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Export SkyLyrics theme profile",
            Filter = "SkyLyrics theme profile (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"{profile.Id}.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(profile, JsonOptions));
            _status.Text = "Theme profile exported.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Theme export failed: {exception.Message}";
        }
    }

    private static UIElement Section(string text) => new TextBlock
    {
        Text = text,
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        Foreground = Brush("#A8E6CF"),
        Margin = new Thickness(0, 10, 0, 9)
    };

    private static UIElement Field(string label, Control control)
    {
        control.Margin = new Thickness(0, 0, 0, 10);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(control);
        return panel;
    }

    private static TextBox ColorInput(TextBox box)
    {
        box.MaxLength = 7;
        box.FontFamily = new FontFamily("Cascadia Mono");
        box.Padding = new Thickness(8, 6, 8, 6);
        box.Background = Brush("#0B0D10");
        box.Foreground = Brush("#F4F5F2");
        box.BorderBrush = Brush("#28323A");
        return box;
    }

    private static void AddAction(Grid grid, string text, int row, int column, RoutedEventHandler handler)
    {
        var button = MakeButton(text);
        button.Margin = new Thickness(column == 0 ? 0 : 5, row == 0 ? 0 : 5, column == 0 ? 5 : 0, row == 0 ? 5 : 0);
        button.Click += handler;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private static void AddFooterButton(Panel panel, string text, RoutedEventHandler handler, bool primary = false)
    {
        var button = MakeButton(text);
        button.Margin = new Thickness(0, 0, 8, 0);
        if (primary)
        {
            button.Background = Brush("#A8E6CF");
            button.Foreground = Brush("#101613");
        }
        button.Click += handler;
        panel.Children.Add(button);
    }

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(12, 8, 12, 8),
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        Foreground = Brush("#F4F5F2"),
        Background = Brush("#1B2229"),
        BorderBrush = Brush("#28323A"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)!);
        brush.Freeze();
        return brush;
    }
}
