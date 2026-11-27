using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SkyLyrics.Infrastructure.Lyrics;
using System.IO;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignmentKind = System.Windows.HorizontalAlignment;
using OrientationKind = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace SkyLyrics.Desktop;

public sealed class LyricsEditorWindow : Window
{
    private readonly TextBox _editor = new();
    private readonly TextBlock _status = new();
    private readonly LrcParser _parser = new();
    private readonly string _suggestedFileName;

    public string? SavedFilePath { get; private set; }

    public LyricsEditorWindow(string sourceText, string? title, string? artist)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        _suggestedFileName = BuildFileName(title, artist);
        Title = "SkyLyrics · Lyrics Studio";
        Width = 980;
        Height = 760;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush("#0B0D10");
        Foreground = Brush("#F4F5F2");
        FontFamily = new FontFamily("Segoe UI Variable Display");

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock
        {
            Text = "LYRICS STUDIO",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Edit and export synchronized lyrics",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 0, 0)
        });
        heading.Children.Add(new TextBlock
        {
            Text = $"{title ?? "Untitled track"}  ·  {artist ?? "Unknown artist"}",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "LRC timestamps are preserved as entered. Validate before saving; this editor does not invent or distribute word-level timestamps.",
            FontSize = 10,
            Foreground = Brush("#A8B1B6"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        root.Children.Add(heading);

        _editor.Text = sourceText;
        _editor.AcceptsReturn = true;
        _editor.AcceptsTab = true;
        _editor.TextWrapping = TextWrapping.NoWrap;
        _editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _editor.FontFamily = new FontFamily("Cascadia Mono");
        _editor.FontSize = 12;
        _editor.Padding = new Thickness(14);
        _editor.Background = Brush("#12161B");
        _editor.Foreground = Brush("#E7EEE9");
        _editor.BorderBrush = Brush("#28323A");
        _editor.BorderThickness = new Thickness(1);
        _editor.MaxLength = 2_000_000;
        Grid.SetRow(_editor, 1);
        root.Children.Add(_editor);

        var footer = new Grid { Margin = new Thickness(0, 15, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _status.Text = "Edit the LRC source, then validate it before saving.";
        _status.FontSize = 10;
        _status.Foreground = Brush("#A8B1B6");
        _status.TextWrapping = TextWrapping.Wrap;
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_status);

        var actions = new StackPanel
        {
            Orientation = OrientationKind.Horizontal,
            HorizontalAlignment = HorizontalAlignmentKind.Right
        };
        var cancel = CreateButton("Cancel", false);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_, _) => Close();
        var validate = CreateButton("Validate", false);
        validate.Margin = new Thickness(0, 0, 8, 0);
        validate.Click += (_, _) => ValidateSource(showDiagnostics: true);
        var save = CreateButton("Save LRC", true);
        save.Click += Save_Click;
        actions.Children.Add(cancel);
        actions.Children.Add(validate);
        actions.Children.Add(save);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var parsed = ValidateSource(showDiagnostics: false);
        if (parsed is null)
        {
            return;
        }

        if (!parsed.Diagnostics.IsEmpty)
        {
            var details = string.Join(Environment.NewLine, parsed.Diagnostics.Take(5));
            var result = MessageBox.Show(
                this,
                $"The parser reported {parsed.Diagnostics.Length} warning(s):{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}Save this source anyway?",
                "Review LRC warnings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export synchronized lyrics",
            Filter = "LRC lyrics (*.lrc)|*.lrc",
            DefaultExt = ".lrc",
            AddExtension = true,
            FileName = _suggestedFileName,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, _editor.Text);
            SavedFilePath = dialog.FileName;
            DialogResult = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Could not save the selected file: {exception.Message}";
        }
    }

    private SkyLyrics.Core.Lyrics.LyricsDocument? ValidateSource(bool showDiagnostics)
    {
        if (string.IsNullOrWhiteSpace(_editor.Text))
        {
            _status.Text = "The lyrics document is empty.";
            return null;
        }

        try
        {
            var parsed = _parser.Parse(_editor.Text);
            var lineCount = parsed.Lines.Length;
            var textCount = parsed.Lines.Count(line => !string.IsNullOrWhiteSpace(line.Text));
            if (!parsed.Instrumental && textCount == 0 && string.IsNullOrWhiteSpace(parsed.PlainText))
            {
                _status.Text = "No lyric lines were parsed. Check the LRC timestamp format and line content.";
                return null;
            }

            _status.Text = parsed.Diagnostics.IsEmpty
                ? $"Valid source · {lineCount} parsed line(s) · {parsed.SyncMode}."
                : $"Parsed {lineCount} line(s) with {parsed.Diagnostics.Length} warning(s). Review before saving.";

            if (showDiagnostics && !parsed.Diagnostics.IsEmpty)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, parsed.Diagnostics),
                    "LRC parser diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return parsed;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            _status.Text = $"The LRC source could not be parsed: {exception.Message}";
            return null;
        }
    }

    private static string BuildFileName(string? title, string? artist)
    {
        static string Safe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Lyrics";
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "Lyrics" : cleaned;
        }

        return $"{Safe(artist)} - {Safe(title)}.lrc";
    }

    private static Button CreateButton(string text, bool primary) => new()
    {
        Content = text,
        Padding = new Thickness(15, 9, 15, 9),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush("#F4F5F2"),
        Background = Brush(primary ? "#1C302A" : "#1B2229"),
        BorderBrush = Brush(primary ? "#A8E6CF" : "#28323A"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);
}
