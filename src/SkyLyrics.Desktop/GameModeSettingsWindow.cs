using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkyLyrics.Application.Settings;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using Cursor = System.Windows.Input.Cursor;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using ColorConverter = System.Windows.Media.ColorConverter;
using Color = System.Windows.Media.Color;

namespace SkyLyrics.Desktop;

public sealed class GameModeSettingsWindow : Window
{
    private sealed record Choice(string Id, string Label);

    private readonly GameModePreferences _initial;
    private readonly TextBox _widthBox = new();
    private readonly TextBox _heightBox = new();
    private readonly Slider _opacitySlider = new();
    private readonly CheckBox _topmostCheck = new();
    private readonly CheckBox _clickThroughCheck = new();
    private readonly ComboBox _backgroundBox = new();
    private readonly ComboBox _effectBox = new();
    private readonly ComboBox _animationBox = new();
    private readonly TextBlock _opacityValue = new();
    private readonly TextBlock _status = new();

    public GameModePreferences? Result { get; private set; }

    public GameModeSettingsWindow(GameModePreferences current)
    {
        _initial = (current ?? new GameModePreferences()).Normalize();

        Title = "SkyLyrics · Game Mode settings";
        Width = 590;
        Height = 650;
        MinWidth = 520;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush("#12161B");
        Foreground = Brush("#F4F5F2");
        FontFamily = new FontFamily("Segoe UI Variable Display");

        _widthBox.Text = _initial.Width.ToString("0", CultureInfo.InvariantCulture);
        _heightBox.Text = _initial.Height.ToString("0", CultureInfo.InvariantCulture);
        _widthBox.MaxLength = 4;
        _heightBox.MaxLength = 3;
        StyleInput(_widthBox);
        StyleInput(_heightBox);

        _opacitySlider.Minimum = 0.55;
        _opacitySlider.Maximum = 1d;
        _opacitySlider.TickFrequency = 0.05;
        _opacitySlider.IsSnapToTickEnabled = true;
        _opacitySlider.Value = _initial.Opacity;
        _opacitySlider.VerticalAlignment = VerticalAlignment.Center;
        _opacitySlider.ValueChanged += (_, _) =>
            _opacityValue.Text = $"{Math.Round(_opacitySlider.Value * 100d):0}%";
        _opacityValue.Text = $"{Math.Round(_initial.Opacity * 100d):0}%";

        _topmostCheck.Content = "Keep Game Mode above other windows";
        _topmostCheck.IsChecked = _initial.AlwaysOnTop;
        _topmostCheck.Foreground = Brush("#A8B1B6");
        _topmostCheck.Margin = new Thickness(0, 5, 0, 0);

        _clickThroughCheck.Content = "Enable click-through (Ctrl+Alt+Shift+G restores interaction)";
        _clickThroughCheck.IsChecked = _initial.ClickThrough;
        _clickThroughCheck.Foreground = Brush("#A8B1B6");
        _clickThroughCheck.Margin = new Thickness(0, 12, 0, 0);

        _backgroundBox = CreateChoiceBox(
            new[]
            {
                new Choice("aurora", "Aurora Mint"),
                new Choice("ocean", "Deep Ocean"),
                new Choice("violet", "Violet Haze"),
                new Choice("sunset", "Sunset Ember"),
                new Choice("graphite", "Graphite Glass")
            },
            _initial.BackgroundId);

        _effectBox = CreateChoiceBox(
            new[]
            {
                new Choice("none", "Clean · no glow"),
                new Choice("mint-glow", "Mint Halo"),
                new Choice("ice-glow", "Ice Blue"),
                new Choice("soft-shadow", "Soft Depth")
            },
            _initial.TextEffectId);

        _animationBox = CreateChoiceBox(
            new[]
            {
                new Choice("fade", "Soft Fade"),
                new Choice("slide", "Cinematic Slide"),
                new Choice("zoom", "Scale Reveal"),
                new Choice("spring", "Spring Motion"),
                new Choice("snap", "Quick Snap")
            },
            _initial.AnimationId);

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        heading.Children.Add(new TextBlock
        {
            Text = "Game Mode",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F4F5F2")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Size, placement behavior and the overlay's live visual treatment",
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = Brush("#A8B1B6")
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var sections = new StackPanel();
        sections.Children.Add(CreateSection("WINDOW SIZE", CreateGeometryGrid()));
        sections.Children.Add(CreateSection("VISUAL STYLE", CreateVisualGrid()));
        sections.Children.Add(CreateSection("WINDOW BEHAVIOR", CreateBehaviorPanel()));

        var scroll = new ScrollViewer
        {
            Content = sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _status.Text = "Drag the overlay header to move it; drag an edge or corner to resize.";
        _status.FontSize = 9;
        _status.Foreground = Brush("#A8B1B6");
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_status);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var reset = CreateButton("Reset", false);
        reset.Margin = new Thickness(0, 0, 7, 0);
        reset.Click += (_, _) => ResetInputs();
        var cancel = CreateButton("Cancel", false);
        cancel.Margin = new Thickness(0, 0, 7, 0);
        cancel.Click += (_, _) => Close();
        var save = CreateButton("Save settings", true);
        save.Click += Save_Click;
        actions.Children.Add(reset);
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;
    }

    private UIElement CreateGeometryGrid()
    {
        var grid = CreateTwoColumnGrid();
        AddField(grid, "WIDTH  ·  420–1920 PX", _widthBox, 0, 0);
        AddField(grid, "HEIGHT  ·  112–600 PX", _heightBox, 1, 0);
        return grid;
    }

    private UIElement CreateVisualGrid()
    {
        var grid = CreateTwoColumnGrid();
        AddField(grid, "BACKGROUND", _backgroundBox, 0, 0);
        AddField(grid, "TEXT EFFECT", _effectBox, 1, 0);
        AddField(grid, "LINE ANIMATION", _animationBox, 0, 1);
        var opacityPanel = new StackPanel();
        var label = new TextBlock
        {
            Text = "OVERLAY OPACITY",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8B1B6")
        };
        var valueRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_opacitySlider, 0);
        _opacityValue.Margin = new Thickness(10, 0, 0, 0);
        _opacityValue.FontSize = 10;
        _opacityValue.Foreground = Brush("#B7C2BC");
        _opacityValue.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_opacityValue, 1);
        valueRow.Children.Add(_opacitySlider);
        valueRow.Children.Add(_opacityValue);
        opacityPanel.Children.Add(label);
        opacityPanel.Children.Add(valueRow);
        AddField(grid, opacityPanel, 1, 1);
        return grid;
    }

    private UIElement CreateBehaviorPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(_topmostCheck);
        panel.Children.Add(_clickThroughCheck);
        panel.Children.Add(new TextBlock
        {
            Text = "Position is saved when you move or resize the overlay. The overlay can be placed on any connected monitor.",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 10, 0, 0)
        });
        return panel;
    }

    private static Grid CreateTwoColumnGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        return grid;
    }

    private static void AddField(Grid grid, string labelText, Control input, int column, int row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 12) };
        panel.Children.Add(new TextBlock
        {
            Text = labelText,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        panel.Children.Add(input);
        AddField(grid, panel, column, row);
    }

    private static void AddField(Grid grid, UIElement element, int column, int row)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private static Border CreateSection(string title, UIElement content)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(content);

        return new Border
        {
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(14),
            Background = Brush("#1B2229"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1),
            Child = panel
        };
    }

    private static ComboBox CreateChoiceBox(IEnumerable<Choice> choices, string selected)
    {
        var combo = new ComboBox
        {
            ItemsSource = choices.ToArray(),
            DisplayMemberPath = nameof(Choice.Label),
            SelectedValuePath = nameof(Choice.Id),
            SelectedValue = selected,
            MinWidth = 190,
            Height = 34,
            FontSize = 11
        };
        return combo;
    }

    private static void StyleInput(TextBox input)
    {
        input.Width = 120;
        input.Height = 34;
        input.Padding = new Thickness(9, 6, 9, 6);
        input.FontSize = 12;
        input.Background = Brush("#12161B");
        input.Foreground = Brush("#F4F5F2");
        input.BorderBrush = Brush("#28323A");
        input.BorderThickness = new Thickness(1);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_widthBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            width is < 420 or > 1920)
        {
            _status.Text = "Width must be a whole number between 420 and 1920 pixels.";
            return;
        }

        if (!int.TryParse(_heightBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
            height is < 112 or > 600)
        {
            _status.Text = "Height must be a whole number between 112 and 600 pixels.";
            return;
        }

        Result = (_initial with
        {
            Width = width,
            Height = height,
            Opacity = _opacitySlider.Value,
            AlwaysOnTop = _topmostCheck.IsChecked == true,
            ClickThrough = _clickThroughCheck.IsChecked == true,
            BackgroundId = _backgroundBox.SelectedValue as string ?? _initial.BackgroundId,
            TextEffectId = _effectBox.SelectedValue as string ?? _initial.TextEffectId,
            AnimationId = _animationBox.SelectedValue as string ?? _initial.AnimationId
        }).Normalize();

        DialogResult = true;
    }

    private void ResetInputs()
    {
        _widthBox.Text = "1000";
        _heightBox.Text = "176";
        _opacitySlider.Value = 0.96;
        _topmostCheck.IsChecked = true;
        _clickThroughCheck.IsChecked = false;
        _backgroundBox.SelectedValue = "aurora";
        _effectBox.SelectedValue = "mint-glow";
        _animationBox.SelectedValue = "cinematic";
        _status.Text = "Default appearance selected. Press Save settings to apply it.";
    }

    private static Button CreateButton(string label, bool primary)
    {
        return new Button
        {
            Content = label,
            Padding = new Thickness(13, 8, 13, 8),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Background = Brush(primary ? "#1C302A" : "#1B2229"),
            Foreground = Brush("#F4F5F2"),
            BorderBrush = Brush(primary ? "#A8E6CF" : "#28323A"),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);
}
