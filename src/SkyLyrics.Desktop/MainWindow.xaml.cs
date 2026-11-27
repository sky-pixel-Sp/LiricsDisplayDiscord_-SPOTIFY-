using System.Windows;
using Microsoft.Win32;
using SkyLyrics.Desktop.ViewModels;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public MainWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import lyrics",
            Filter = "Lyrics files|*.lrc;*.yaml;*.yml;*.lyricsfile.yaml|LRC files|*.lrc|Lyricsfile YAML|*.yaml;*.yml;*.lyricsfile.yaml",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.LoadLocalLyricsFileAsync(dialog.FileName);
        }
    }
}
