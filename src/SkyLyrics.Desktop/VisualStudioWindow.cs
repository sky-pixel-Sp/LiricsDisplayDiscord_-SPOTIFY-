using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SkyLyrics.Application.Settings;
using SkyLyrics.Desktop.ViewModels;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using Grid = System.Windows.Controls.Grid;
using Image = System.Windows.Controls.Image;
using Slider = System.Windows.Controls.Slider;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SkyLyrics.Desktop;

public sealed class VisualStudioWindow : Window
{
    private sealed record Choice(string Id, string Label);

    private sealed class Particle(Ellipse shape, double x, double y, double speed, double phase)
    {
        public Ellipse Shape { get; } = shape;
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Speed { get; } = speed;
        public double Phase { get; } = phase;
    }

    private readonly ShellViewModel _viewModel;
    private readonly IAppPreferencesStore _preferences;
    private readonly Image _artwork = new();
    private readonly Border _gradientSurface = new();
    private readonly Canvas _particleLayer = new();
    private readonly TextBlock _trackTitle = new();
    private readonly TextBlock _trackSubtitle = new();
    private readonly TextBlock _status = new();
    private readonly ComboBox _modeBox = new();
    private readonly ComboBox _qualityBox = new();
    private readonly Slider _intensitySlider = new();
    private readonly DispatcherTimer _timer = new();
    private readonly List<Particle> _particles = [];
    private VisualStudioPreferences _settings;
    private bool _loaded;
    private bool _updatingControls;

    public VisualStudioWindow(ShellViewModel viewModel, IAppPreferencesStore preferences)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _settings = (_preferences.Current.Visuals ?? new VisualStudioPreferences()).Normalize();

        Title = "SkyLyrics · Visual Studio";
        Width = 1180;
        Height = 820;
        MinWidth = 860;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush("#0B0D10");
        Foreground = Brush("#F4F5F2");
        FontFamily = new FontFamily("Segoe UI Variable Display");

        _timer.Tick += OnAnimationTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        IsVisibleChanged += (_, _) => UpdateAnimationState();
        StateChanged += (_, _) => UpdateAnimationState();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        BuildInterface();
        ApplySettings();
        UpdateTrackContext();
    }

    private void BuildInterface()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "VISUAL STUDIO",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Shape the listening atmosphere",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 0, 0)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Album ambience · fluid gradients · particle fields",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        header.Children.Add(heading);
        _status.Text = "METADATA-REACTIVE · NO AUDIO CAPTURE";
        _status.FontSize = 9;
        _status.FontWeight = FontWeights.Bold;
        _status.Foreground = Brush("#A8E6CF");
        _status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_status, 1);
        header.Children.Add(_status);
        root.Children.Add(header);

        var preview = new Border
        {
            CornerRadius = new CornerRadius(24),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Background = Brush("#12161B")
        };
        var scene = new Grid();
        _artwork.Stretch = Stretch.UniformToFill;
        _artwork.Opacity = 0.34;
        RenderOptions.SetBitmapScalingMode(_artwork, BitmapScalingMode.LowQuality);
        _artwork.Visibility = Visibility.Collapsed;
        scene.Children.Add(_artwork);

        _gradientSurface.Background = CreateGradient();
        _gradientSurface.Opacity = 0.86;
        scene.Children.Add(_gradientSurface);

        scene.Children.Add(new Rectangle
        {
            Fill = new LinearGradientBrush(
                Color.FromArgb(228, 8, 12, 16),
                Color.FromArgb(90, 8, 12, 16),
                90),
            IsHitTestVisible = false
        });

        _particleLayer.IsHitTestVisible = false;
        scene.Children.Add(_particleLayer);

        var context = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 760,
            Margin = new Thickness(45)
        };
        context.Children.Add(new TextBlock
        {
            Text = "SKYLYRICS · LIVE SCENE",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _trackTitle.FontSize = 36;
        _trackTitle.FontWeight = FontWeights.SemiBold;
        _trackTitle.TextAlignment = TextAlignment.Center;
        _trackTitle.TextWrapping = TextWrapping.Wrap;
        _trackTitle.Margin = new Thickness(0, 12, 0, 0);
        context.Children.Add(_trackTitle);

        _trackSubtitle.FontSize = 13;
        _trackSubtitle.Foreground = Brush("#D1DAD4");
        _trackSubtitle.TextAlignment = TextAlignment.Center;
        _trackSubtitle.TextWrapping = TextWrapping.Wrap;
        _trackSubtitle.Margin = new Thickness(0, 8, 0, 0);
        context.Children.Add(_trackSubtitle);
        scene.Children.Add(context);
        preview.Child = scene;
        Grid.SetRow(preview, 1);
        root.Children.Add(preview);

        var controlsBorder = new Border
        {
            Padding = new Thickness(16),
            Margin = new Thickness(0, 14, 0, 0),
            CornerRadius = new CornerRadius(18),
            Background = Brush("#12161B"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1)
        };
        var controls = new Grid();
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _modeBox.ItemsSource = new[]
        {
            new Choice("album-ambient", "Album ambience"),
            new Choice("fluid-gradient", "Fluid gradient"),
            new Choice("particle-field", "Particle field")
        };
        _modeBox.DisplayMemberPath = nameof(Choice.Label);
        _modeBox.SelectedValuePath = nameof(Choice.Id);
        _modeBox.MinHeight = 36;
        _modeBox.SelectionChanged += (_, _) => OnControlChanged();

        _qualityBox.ItemsSource = new[]
        {
            new Choice("eco", "Eco"),
            new Choice("balanced", "Balanced"),
            new Choice("high", "High"),
            new Choice("ultra", "Ultra")
        };
        _qualityBox.DisplayMemberPath = nameof(Choice.Label);
        _qualityBox.SelectedValuePath = nameof(Choice.Id);
        _qualityBox.MinHeight = 36;
        _qualityBox.SelectionChanged += (_, _) => OnControlChanged();

        _intensitySlider.Minimum = 0;
        _intensitySlider.Maximum = 100;
        _intensitySlider.TickFrequency = 5;
        _intensitySlider.ValueChanged += (_, _) => OnIntensityChanged();

        controls.Children.Add(CreateField("VISUAL MODE", _modeBox, 0));
        controls.Children.Add(CreateField("QUALITY GOVERNOR", _qualityBox, 1));
        controls.Children.Add(CreateField("EFFECT INTENSITY", _intensitySlider, 2));

        var save = CreateButton("SAVE PROFILE");
        save.Click += Save_Click;
        Grid.SetColumn(save, 3);
        save.VerticalAlignment = VerticalAlignment.Bottom;
        controls.Children.Add(save);
        controlsBorder.Child = controls;
        Grid.SetRow(controlsBorder, 2);
        root.Children.Add(controlsBorder);
        Content = root;
    }

    private static UIElement CreateField(string label, Control control, int column)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 0, 0, 7)
        });
        panel.Children.Add(control);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        RebuildParticles();
        UpdateAnimationState();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.AlbumArtwork)
            or nameof(ShellViewModel.StatusTitle)
            or nameof(ShellViewModel.ArtistLine)
            or nameof(ShellViewModel.PlaybackStatusLabel))
        {
            UpdateTrackContext();
        }
    }

    private void UpdateTrackContext()
    {
        _trackTitle.Text = _viewModel.StatusTitle;
        _trackSubtitle.Text = $"{_viewModel.ArtistLine}  ·  {_viewModel.PlaybackStatusLabel}";
        _artwork.Source = _viewModel.AlbumArtwork;
        UpdateVisualLayers();
    }

    private void OnControlChanged()
    {
        if (_updatingControls) return;

        if (_modeBox.SelectedValue is string mode)
        {
            _settings = _settings with { ModeId = mode };
        }

        if (_qualityBox.SelectedValue is string quality)
        {
            _settings = _settings with { QualityId = quality };
        }

        ApplySettings();
    }

    private void OnIntensityChanged()
    {
        if (_updatingControls || !_loaded) return;
        _settings = (_settings with { Intensity = (int)Math.Round(_intensitySlider.Value) }).Normalize();
        ApplyVisualIntensity();
    }

    private void ApplySettings()
    {
        _settings = _settings.Normalize();
        _updatingControls = true;
        _modeBox.SelectedValue = _settings.ModeId;
        _qualityBox.SelectedValue = _settings.QualityId;
        _intensitySlider.Value = _settings.Intensity;
        _updatingControls = false;

        UpdateVisualLayers();
        RebuildParticles();
        UpdateAnimationState();
    }

    private void UpdateVisualLayers()
    {
        _artwork.Visibility = _viewModel.HasAlbumArtwork && _settings.ModeId == "album-ambient"
            ? Visibility.Visible
            : Visibility.Collapsed;
        _gradientSurface.Visibility = _settings.ModeId == "fluid-gradient"
            ? Visibility.Visible
            : Visibility.Collapsed;
        _particleLayer.Visibility = _settings.ModeId == "particle-field"
            ? Visibility.Visible
            : Visibility.Collapsed;

        _status.Text = _viewModel.HasAlbumArtwork
            ? "METADATA-REACTIVE · REAL MEDIA SESSION ARTWORK"
            : "METADATA-REACTIVE · NO ARTWORK PROVIDED";
        ApplyVisualIntensity();
    }

    private void ApplyVisualIntensity()
    {
        _gradientSurface.Opacity = 0.22d + (_settings.Intensity / 100d * 0.72d);
        foreach (var particle in _particles)
        {
            particle.Shape.Opacity = 0.12d + (_settings.Intensity / 100d * 0.72d);
        }
    }

    private void RebuildParticles()
    {
        _particleLayer.Children.Clear();
        _particles.Clear();

        if (_settings.ModeId != "particle-field") return;

        var random = new Random(7123);
        for (var index = 0; index < _settings.ParticleBudget; index++)
        {
            var size = random.NextDouble() * 3.8d + 1.4d;
            var shape = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brush("#A8E6CF"),
                Opacity = 0.25d,
                IsHitTestVisible = false
            };
            _particleLayer.Children.Add(shape);
            _particles.Add(new Particle(
                shape,
                random.NextDouble(),
                random.NextDouble(),
                0.0007d + random.NextDouble() * 0.0015d,
                random.NextDouble() * Math.PI * 2d));
        }

        ApplyVisualIntensity();
    }

    private void UpdateAnimationState()
    {
        if (!_loaded || !IsVisible || WindowState == WindowState.Minimized ||
            _preferences.Current.ReduceMotion || _settings.ModeId == "album-ambient")
        {
            _timer.Stop();
            return;
        }

        _timer.Interval = TimeSpan.FromMilliseconds(1000d / _settings.FrameRateCap);
        _timer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_settings.ModeId == "particle-field")
        {
            var width = Math.Max(1d, _particleLayer.ActualWidth);
            var height = Math.Max(1d, _particleLayer.ActualHeight);
            var seconds = Environment.TickCount64 / 1000d;

            foreach (var particle in _particles)
            {
                particle.Y -= particle.Speed;
                if (particle.Y < -0.05d)
                {
                    particle.Y = 1.05d;
                    particle.X = (particle.X + 0.173d) % 1d;
                }

                var drift = Math.Sin(seconds * 0.28d + particle.Phase) * 0.035d;
                Canvas.SetLeft(particle.Shape, Math.Clamp((particle.X + drift) * width, 0d, width));
                Canvas.SetTop(particle.Shape, Math.Clamp(particle.Y * height, 0d, height));
            }
        }
        else if (_settings.ModeId == "fluid-gradient" &&
                 _gradientSurface.Background is LinearGradientBrush brush &&
                 brush.Transform is RotateTransform rotation)
        {
            rotation.Angle = (rotation.Angle + 0.12d) % 360d;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _preferences.SaveAsync(_preferences.Current with
            {
                Visuals = _settings.Normalize()
            });
            _status.Text = "VISUAL PROFILE SAVED";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Could not save visual profile: {exception.Message}",
                "Visual Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static LinearGradientBrush CreateGradient()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            Transform = new RotateTransform(0, 0.5, 0.5)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#235B4D")!, 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#342B63")!, 0.48));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#101C2A")!, 1));
        return brush;
    }

    private static Button CreateButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(16, 10, 16, 10),
        FontSize = 10,
        FontWeight = FontWeights.Bold,
        Foreground = Brush("#F4F5F2"),
        Background = Brush("#1C302A"),
        BorderBrush = Brush("#A8E6CF"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);
}
