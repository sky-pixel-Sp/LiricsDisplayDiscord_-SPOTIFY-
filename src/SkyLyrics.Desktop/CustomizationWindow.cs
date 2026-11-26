using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SkyLyrics.Desktop;

public sealed class CustomizationWindow : Window
{
    private readonly ListBox _animations = CreateListBox();
    private readonly ListBox _effects = CreateListBox();
    private readonly ListBox _backgrounds = CreateListBox();
    private readonly TextBlock _previewText = new();
    private readonly Border _previewSurface = new();
    private readonly Action<AppearanceSettings> _apply;
    private readonly AppearanceSettings _initial;

    public CustomizationWindow(AppearanceSettings current, Action<AppearanceSettings> apply)
    {
        _initial = current;
        _apply = apply;
        Title = "SkyLyrics · Appearance Studio";
        Width = 1040;
        Height = 760;
        MinWidth = 900;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#101916");
        Foreground = Brush("#EDF5F0");
        ResizeMode = ResizeMode.CanResize;

        _animations.ItemsSource = AppearanceCatalog.Animations;
        _animations.DisplayMemberPath = nameof(AnimationPreset.Name);
        _animations.SelectedValuePath = nameof(AnimationPreset.Id);
        _animations.SelectedValue = current.AnimationId;

        _effects.ItemsSource = AppearanceCatalog.Effects;
        _effects.DisplayMemberPath = nameof(EffectPreset.Name);
        _effects.SelectedValuePath = nameof(EffectPreset.Id);
        _effects.SelectedValue = current.EffectId;

        _backgrounds.ItemsSource = AppearanceCatalog.Backgrounds;
        _backgrounds.DisplayMemberPath = nameof(BackgroundPreset.Name);
        _backgrounds.SelectedValuePath = nameof(BackgroundPreset.Id);
        _backgrounds.SelectedValue = current.BackgroundId;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Appearance Studio",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F0F7F2")
        });
        header.Children.Add(new TextBlock
        {
            Text = "Choose each category independently. Your choices are saved on this PC.",
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = Brush("#9EAEA5")
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.Children.Add(CreateCategory("ANIMATIONS · 30", "Line transitions", _animations));
        var effects = CreateCategory("EFFECTS · 30", "Active-line lighting and depth", _effects);
        Grid.SetColumn(effects, 1);
        columns.Children.Add(effects);
        var backgrounds = CreateCategory("BACKGROUNDS · 30", "Independent gradient surfaces", _backgrounds);
        Grid.SetColumn(backgrounds, 2);
        columns.Children.Add(backgrounds);
        Grid.SetRow(columns, 2);
        root.Children.Add(columns);

        _previewSurface.CornerRadius = new CornerRadius(15);
        _previewSurface.BorderThickness = new Thickness(1);
        _previewSurface.BorderBrush = Brush("#52645B");
        _previewSurface.Padding = new Thickness(18);
        _previewText.Text = "♫  A line of synchronized lyrics";
        _previewText.FontSize = 19;
        _previewText.FontWeight = FontWeights.SemiBold;
        _previewText.TextAlignment = TextAlignment.Center;
        _previewText.Foreground = Brush("#F4FFF9");
        _previewSurface.Child = _previewText;
        _previewSurface.Background = AppearanceCatalog.CreateBackgroundBrush(current.BackgroundId);

        var previewPanel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        previewPanel.Children.Add(new TextBlock
        {
            Text = "LIVE PREVIEW",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#90A198"),
            Margin = new Thickness(2, 0, 0, 8)
        });
        previewPanel.Children.Add(_previewSurface);
        Grid.SetRow(previewPanel, 3);
        root.Children.Add(previewPanel);

        var footer = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var counts = new TextBlock
        {
            Text = "30 animations   ·   30 effects   ·   30 backgrounds",
            FontSize = 11,
            Foreground = Brush("#9EAEA5"),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(counts, Dock.Left);
        footer.Children.Add(counts);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var reset = CreateButton("Reset", false);
        reset.Margin = new Thickness(0, 0, 8, 0);
        reset.Click += (_, _) => SelectSettings(AppearanceSettings.Default);
        var cancel = CreateButton("Cancel", false);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_, _) => Close();
        var applyButton = CreateButton("Apply appearance", true);
        applyButton.Click += (_, _) =>
        {
            var settings = SelectedSettings();
            _apply(settings);
            Close();
        };
        buttons.Children.Add(reset);
        buttons.Children.Add(cancel);
        buttons.Children.Add(applyButton);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        _animations.SelectionChanged += SelectionChanged;
        _effects.SelectionChanged += SelectionChanged;
        _backgrounds.SelectionChanged += SelectionChanged;

        Content = root;
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var settings = SelectedSettings();
        _previewSurface.Background = AppearanceCatalog.CreateBackgroundBrush(settings.BackgroundId);
        _previewText.Effect = AppearanceCatalog.CreateTextEffect(settings.EffectId);
        _previewText.Text = $"♫  {AppearanceCatalog.GetAnimation(settings.AnimationId).Name.Replace(" · ", " / ", StringComparison.Ordinal)}";
    }

    private AppearanceSettings SelectedSettings() => new(
        _animations.SelectedValue as string ?? _initial.AnimationId,
        _effects.SelectedValue as string ?? _initial.EffectId,
        _backgrounds.SelectedValue as string ?? _initial.BackgroundId);

    private void SelectSettings(AppearanceSettings settings)
    {
        _animations.SelectedValue = settings.AnimationId;
        _effects.SelectedValue = settings.EffectId;
        _backgrounds.SelectedValue = settings.BackgroundId;
    }

    private static Border CreateCategory(string title, string subtitle, ListBox list)
    {
        var panel = new Border
        {
            Background = Brush("#19241F"),
            BorderBrush = Brush("#304139"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 10, 0)
        };

        var content = new DockPanel();
        var heading = new StackPanel { Margin = new Thickness(2, 0, 2, 12) };
        heading.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#BFECD8")
        });
        heading.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = Brush("#90A198")
        });
        DockPanel.SetDock(heading, Dock.Top);
        content.Children.Add(heading);

        list.Margin = new Thickness(0);
        list.Background = Brush("#121B17");
        list.BorderBrush = Brush("#304139");
        list.BorderThickness = new Thickness(1);
        list.Foreground = Brush("#E7F0EB");
        list.FontSize = 11;
        list.Padding = new Thickness(4);
        list.SelectionMode = SelectionMode.Single;
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        content.Children.Add(list);
        panel.Child = content;
        return panel;
    }

    private static ListBox CreateListBox() => new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };

    private static Button CreateButton(string label, bool primary)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(15, 9, 15, 9),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1),
            BorderBrush = Brush(primary ? "#69D8B1" : "#43564B"),
            Background = Brush(primary ? "#256D52" : "#1A2821"),
            Foreground = Brush("#F2FFF7"),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        return button;
    }

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value));
}
