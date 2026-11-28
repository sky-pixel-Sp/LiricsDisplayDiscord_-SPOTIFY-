using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignmentKind = System.Windows.HorizontalAlignment;
using OrientationKind = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;

namespace SkyLyrics.Desktop;

public sealed class TranslationLanguageWindow : Window
{
    private readonly TextBox _language = new();

    public string LanguageLabel => _language.Text.Trim();

    public TranslationLanguageWindow()
    {
        Title = "SkyLyrics · Translation language";
        Width = 440;
        Height = 250;
        MinWidth = 400;
        MinHeight = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#12161B");
        Foreground = Brush("#F4F5F2");
        FontFamily = (System.Windows.Application.Current!.Resources["PoppinsFontFamily"] as System.Windows.Media.FontFamily ?? new System.Windows.Media.FontFamily("Segoe UI Variable Display"));

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(new TextBlock
        {
            Text = "TRANSLATION LANGUAGE",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });

        var hint = new TextBlock
        {
            Text = "Enter the language label used for this local file, for example Indonesian or Japanese. SkyLyrics does not guess or generate translations.",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 9, 0, 12)
        };
        Grid.SetRow(hint, 1);
        root.Children.Add(hint);

        _language.MaxLength = 64;
        _language.Padding = new Thickness(10, 8, 10, 8);
        _language.FontSize = 12;
        _language.Background = Brush("#0B0D10");
        _language.Foreground = Brush("#F4F5F2");
        _language.BorderBrush = Brush("#28323A");
        Grid.SetRow(_language, 2);
        root.Children.Add(_language);

        var actions = new StackPanel
        {
            Orientation = OrientationKind.Horizontal,
            HorizontalAlignment = HorizontalAlignmentKind.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var cancel = CreateButton("Cancel");
        cancel.Click += (_, _) => Close();
        var confirm = CreateButton("Continue");
        confirm.Margin = new Thickness(8, 0, 0, 0);
        confirm.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(LanguageLabel))
            {
                MessageBox.Show(this, "Enter a language label.", "Language required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        };
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        Content = root;
        Loaded += (_, _) =>
        {
            _language.Focus();
            _language.SelectAll();
        };
    }

    private static Button CreateButton(string caption) => new()
    {
        Content = caption,
        Padding = new Thickness(13, 8, 13, 8),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Background = Brush("#1C302A"),
        Foreground = Brush("#F4F5F2"),
        BorderBrush = Brush("#A8E6CF"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);
}
