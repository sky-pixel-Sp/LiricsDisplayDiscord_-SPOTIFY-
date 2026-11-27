using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveLyricText))
        {
            Dispatcher.BeginInvoke(new Action(AnimateActiveLyric));
        }
        else if (e.PropertyName == nameof(ShellViewModel.AlbumArtwork))
        {
            Dispatcher.BeginInvoke(new Action(AnimateArtwork));
        }
    }

    private void AnimateActiveLyric()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            ActiveLyricTextBlock.BeginAnimation(OpacityProperty, null);
            ActiveLyricTextBlock.Opacity = 1d;
            if (ActiveLyricTextBlock.RenderTransform is TranslateTransform reducedMotionTransform)
            {
                reducedMotionTransform.BeginAnimation(TranslateTransform.YProperty, null);
                reducedMotionTransform.Y = 0d;
            }

            return;
        }

        ActiveLyricTextBlock.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.42d, 1d, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        if (ActiveLyricTextBlock.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(11d, 0d, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
    }

    private void AnimateArtwork()
    {
        if (!SystemParameters.ClientAreaAnimation || !_viewModel.HasAlbumArtwork)
        {
            ArtworkBackdrop.BeginAnimation(OpacityProperty, null);
            ArtworkBackdrop.Opacity = _viewModel.HasAlbumArtwork ? 0.15d : 0d;
            return;
        }

        ArtworkBackdrop.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0d, 0.15d, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
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
