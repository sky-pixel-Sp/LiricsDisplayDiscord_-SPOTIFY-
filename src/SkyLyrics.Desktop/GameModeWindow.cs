using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SkyLyrics.Application.Settings;
using SkyLyrics.Desktop.ViewModels;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Cursor = System.Windows.Input.Cursor;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using Panel = System.Windows.Controls.Panel;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SkyLyrics.Desktop;

public sealed class GameModeWindow : Window
{
    [Flags]
    private enum ResizeEdges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    private readonly ShellViewModel _viewModel;
    private readonly Action<GameModePreferences> _persist;
    private readonly Border _surface = new();
    private readonly TextBlock _lyric = new();
    private readonly TextBlock _meta = new();
    private readonly TextBlock _status = new();
    private readonly Button _pinButton = new();
    private readonly ScaleTransform _scale = new(1d, 1d);
    private readonly TranslateTransform _translate = new();
    private readonly DispatcherTimer _geometryTimer;
    private GameModePreferences _settings;
    private string _lastLyric = string.Empty;
    private bool _applyingSettings;

    public GameModeWindow(
        ShellViewModel viewModel,
        GameModePreferences settings,
        Action<GameModePreferences> persist)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _settings = (settings ?? new GameModePreferences()).Normalize();

        Title = "SkyLyrics · Game Mode";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = _settings.AlwaysOnTop;
        MinWidth = 420;
        MinHeight = 112;
        MaxWidth = 1920;
        MaxHeight = 600;
        Width = _settings.Width;
        Height = _settings.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var workArea = SystemParameters.WorkArea;
        Left = _settings.Left == -1d ? workArea.Left + (workArea.Width - Width) / 2d : _settings.Left;
        Top = _settings.Top == -1d ? workArea.Top + Math.Max(24d, (workArea.Height - Height) * 0.24d) : _settings.Top;

        _geometryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _geometryTimer.Tick += GeometryTimer_Tick;
        LocationChanged += (_, _) => ScheduleGeometrySave();
        SizeChanged += (_, _) => ScheduleGeometrySave();
        Closed += OnClosed;

        BuildVisualTree();
        ApplySettings(_settings);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateScene();
    }

    private void BuildVisualTree()
    {
        var root = new Grid { Margin = new Thickness(8) };
        _surface.CornerRadius = new CornerRadius(20);
        _surface.BorderThickness = new Thickness(1.2);
        _surface.BorderBrush = MakeGradient("#A8E6CF", "#526D62");
        _surface.Padding = new Thickness(22, 14, 22, 14);
        _surface.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 22,
            ShadowDepth = 4,
            Opacity = 0.44,
            RenderingBias = RenderingBias.Performance
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dragSurface = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 1, 12, 5),
            Cursor = Cursors.SizeAll
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var mark = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = Brush("#A8E6CF"),
            Margin = new Thickness(0, 0, 9, 0)
        };
        brand.Children.Add(mark);
        brand.Children.Add(new TextBlock
        {
            Text = "SKYLYRICS",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F0F4F0"),
            VerticalAlignment = VerticalAlignment.Center
        });
        brand.Children.Add(new TextBlock
        {
            Text = "  /  GAME MODE",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#A8B1B6"),
            VerticalAlignment = VerticalAlignment.Center
        });
        dragSurface.Child = brand;
        dragSurface.MouseLeftButtonDown += DragSurface_MouseLeftButtonDown;
        header.Children.Add(dragSurface);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _pinButton.Content = _settings.AlwaysOnTop ? "PINNED" : "UNPINNED";
        _pinButton.ToolTip = "Toggle always-on-top";
        _pinButton.Click += (_, _) => UpdateSettings(_settings with { AlwaysOnTop = !_settings.AlwaysOnTop });
        StyleHeaderButton(_pinButton, "#1C302A", "#A8E6CF");
        var settingsButton = CreateHeaderButton("⚙", "Game Mode settings");
        settingsButton.Click += (_, _) => OpenSettings();
        var closeButton = CreateHeaderButton("×", "Close Game Mode");
        closeButton.Click += (_, _) => Close();
        actions.Children.Add(_pinButton);
        actions.Children.Add(settingsButton);
        actions.Children.Add(closeButton);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var lyricPanel = new Grid { Margin = new Thickness(10, 5, 10, 7) };
        _lyric.Text = "Waiting for synchronized lyrics";
        _lyric.FontSize = 34;
        _lyric.FontWeight = FontWeights.SemiBold;
        _lyric.TextAlignment = TextAlignment.Center;
        _lyric.TextWrapping = TextWrapping.Wrap;
        _lyric.TextTrimming = TextTrimming.CharacterEllipsis;
        _lyric.MaxHeight = 110;
        _lyric.VerticalAlignment = VerticalAlignment.Center;
        _lyric.Foreground = Brush("#F4F5F2");
        _lyric.RenderTransformOrigin = new Point(0.5d, 0.5d);
        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_translate);
        _lyric.RenderTransform = transforms;
        lyricPanel.Children.Add(_lyric);
        Grid.SetRow(lyricPanel, 1);
        layout.Children.Add(lyricPanel);

        var footer = new Grid { Margin = new Thickness(1, 3, 1, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _meta.FontSize = 9;
        _meta.Foreground = Brush("#BCC7C3");
        _meta.TextTrimming = TextTrimming.CharacterEllipsis;
        _meta.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_meta);
        _status.FontSize = 9;
        _status.Foreground = Brush("#A8E6CF");
        _status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_status, 1);
        footer.Children.Add(_status);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        _surface.Child = layout;
        root.Children.Add(_surface);

        AddResizeHandle(root, ResizeEdges.Left, 8, null, HorizontalAlignment.Left, VerticalAlignment.Stretch, new Thickness(0, 22, 0, 22), Cursors.SizeWE);
        AddResizeHandle(root, ResizeEdges.Right, 8, null, HorizontalAlignment.Right, VerticalAlignment.Stretch, new Thickness(0, 22, 0, 22), Cursors.SizeWE);
        AddResizeHandle(root, ResizeEdges.Top, null, 8, HorizontalAlignment.Stretch, VerticalAlignment.Top, new Thickness(22, 0, 22, 0), Cursors.SizeNS);
        AddResizeHandle(root, ResizeEdges.Bottom, null, 8, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, new Thickness(22, 0, 22, 0), Cursors.SizeNS);
        AddResizeHandle(root, ResizeEdges.Left | ResizeEdges.Top, 20, 20, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(0), Cursors.SizeNWSE);
        AddResizeHandle(root, ResizeEdges.Right | ResizeEdges.Top, 20, 20, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0), Cursors.SizeNESW);
        AddResizeHandle(root, ResizeEdges.Left | ResizeEdges.Bottom, 20, 20, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(0), Cursors.SizeNESW);
        AddResizeHandle(root, ResizeEdges.Right | ResizeEdges.Bottom, 20, 20, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0), Cursors.SizeNWSE);

        Content = root;
    }

    private void AddResizeHandle(
        Grid root,
        ResizeEdges edges,
        double? width,
        double? height,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        Thickness margin,
        Cursor cursor)
    {
        var thumb = new Thumb
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = margin,
            Cursor = cursor
        };
        if (width.HasValue) thumb.Width = width.Value;
        if (height.HasValue) thumb.Height = height.Value;
        thumb.DragDelta += (_, e) => Resize(edges, e);
        Panel.SetZIndex(thumb, 10);
        root.Children.Add(thumb);
    }

    private void Resize(ResizeEdges edges, DragDeltaEventArgs e)
    {
        if (edges.HasFlag(ResizeEdges.Left))
        {
            var next = Math.Max(MinWidth, Width - e.HorizontalChange);
            Left += Width - next;
            Width = next;
        }
        else if (edges.HasFlag(ResizeEdges.Right))
        {
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        }

        if (edges.HasFlag(ResizeEdges.Top))
        {
            var next = Math.Max(MinHeight, Height - e.VerticalChange);
            Top += Height - next;
            Height = next;
        }
        else if (edges.HasFlag(ResizeEdges.Bottom))
        {
            Height = Math.Max(MinHeight, Height + e.VerticalChange);
        }
    }

    private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void OpenSettings()
    {
        var dialog = new GameModeSettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } updated)
        {
            UpdateSettings(updated);
        }
    }

    private void UpdateSettings(GameModePreferences settings)
    {
        _settings = settings.Normalize();
        ApplySettings(_settings);
        _persist(_settings);
    }

    public GameModePreferences Settings => _settings;

    public void ApplySettings(GameModePreferences settings)
    {
        _applyingSettings = true;
        _settings = settings.Normalize();
        Width = _settings.Width;
        Height = _settings.Height;
        if (_settings.Left != -1d) Left = _settings.Left;
        if (_settings.Top != -1d) Top = _settings.Top;
        Topmost = _settings.AlwaysOnTop;
        Opacity = _settings.Opacity;
        _pinButton.Content = _settings.AlwaysOnTop ? "PINNED" : "UNPINNED";
        _surface.Background = BackgroundBrush(_settings.BackgroundId);
        _lyric.Effect = TextEffect(_settings.TextEffectId);
        _applyingSettings = false;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ActiveLyricText)
            or nameof(ShellViewModel.StageEmptyStateText)
            or nameof(ShellViewModel.SyncPositionLabel)
            or nameof(ShellViewModel.SyncStatusLabel)
            or nameof(ShellViewModel.StatusTitle)
            or nameof(ShellViewModel.ArtistLine))
        {
            UpdateScene();
        }
    }

    private void UpdateScene()
    {
        var next = _viewModel.HasActiveLine
            ? _viewModel.ActiveLyricText
            : _viewModel.StageEmptyStateText;

        if (string.IsNullOrWhiteSpace(next))
        {
            next = "No synchronized lyric is active.";
        }

        _meta.Text = $"{_viewModel.StatusTitle}  ·  {_viewModel.ArtistLine}";
        _status.Text = $"{_viewModel.SyncStatusLabel}  ·  {_viewModel.SyncPositionLabel}";

        if (string.Equals(next, _lastLyric, StringComparison.Ordinal)) return;
        _lastLyric = next;
        _lyric.Text = next;
        _lyric.FontSize = _viewModel.HasActiveLine ? Math.Clamp(Height * 0.205d, 25d, 48d) : 20d;
        AnimateLyric();
    }

    private void AnimateLyric()
    {
        var duration = _settings.AnimationId switch
        {
            "snap" => 150,
            "fade" => 260,
            "slide" => 340,
            "zoom" => 360,
            "spring" => 480,
            _ => 390
        };

        var fromY = _settings.AnimationId switch
        {
            "slide" or "cinematic" => 17d,
            "spring" => 11d,
            _ => 0d
        };
        var fromScale = _settings.AnimationId switch
        {
            "zoom" or "spring" or "cinematic" => 0.94d,
            _ => 1d
        };
        var ease = _settings.AnimationId == "spring"
            ? (EasingFunctionBase)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22d }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        _lyric.Opacity = 0.24d;
        _scale.ScaleX = fromScale;
        _scale.ScaleY = fromScale;
        _translate.Y = fromY;

        _lyric.BeginAnimation(OpacityProperty, new DoubleAnimation(0.24d, 1d, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = ease
        });
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, 1d, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = ease
        });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, 1d, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = ease
        });
        _translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0d, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = ease
        });
    }

    private static Brush BackgroundBrush(string preset)
    {
        var colors = preset switch
        {
            "ocean" => ("#11181B", "#344F49", "#567B6C"),
            "violet" => ("#25242F", "#4A455F", "#756273"),
            "sunset" => ("#241A1B", "#59403C", "#8C6751"),
            "graphite" => ("#10171A", "#323F42", "#56615F"),
            _ => ("#111C1B", "#2D5B4D", "#608D74")
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(ColorFrom(colors.Item1), 0));
        brush.GradientStops.Add(new GradientStop(ColorFrom(colors.Item2), 0.54));
        brush.GradientStops.Add(new GradientStop(ColorFrom(colors.Item3), 1));
        return brush;
    }

    private static Brush MakeGradient(string first, string second)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(ColorFrom(first), 0));
        brush.GradientStops.Add(new GradientStop(ColorFrom(second), 1));
        return brush;
    }

    private static Effect? TextEffect(string preset) => preset switch
    {
        "mint-glow" => new DropShadowEffect { Color = ColorFrom("#A8E6CF"), BlurRadius = 15, ShadowDepth = 0, Opacity = 0.72, RenderingBias = RenderingBias.Performance },
        "ice-glow" => new DropShadowEffect { Color = ColorFrom("#A3CFCB"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.7, RenderingBias = RenderingBias.Performance },
        "soft-shadow" => new DropShadowEffect { Color = Colors.Black, BlurRadius = 9, ShadowDepth = 3, Opacity = 0.42, RenderingBias = RenderingBias.Performance },
        _ => null
    };

    private static Color ColorFrom(string value) =>
        (Color)ColorConverter.ConvertFromString(value)!;

    private static SolidColorBrush Brush(string value) => new(ColorFrom(value));

    private static Button CreateHeaderButton(string content, string toolTip)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = toolTip,
            MinWidth = 32,
            Height = 28,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#E8F0EB"),
            Background = Brush("#253833"),
            BorderBrush = Brush("#637B70"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        return button;
    }

    private static void StyleHeaderButton(Button button, string background, string border)
    {
        button.MinWidth = 62;
        button.Height = 28;
        button.Padding = new Thickness(8, 2, 8, 2);
        button.Margin = new Thickness(4, 0, 0, 0);
        button.FontSize = 9;
        button.FontWeight = FontWeights.Bold;
        button.Foreground = Brush("#E8F0EB");
        button.Background = Brush(background);
        button.BorderBrush = Brush(border);
        button.BorderThickness = new Thickness(1);
        button.Cursor = Cursors.Hand;
    }

    private void ScheduleGeometrySave()
    {
        if (_applyingSettings || !IsLoaded) return;
        _geometryTimer.Stop();
        _geometryTimer.Start();
    }

    private void GeometryTimer_Tick(object? sender, EventArgs e)
    {
        _geometryTimer.Stop();
        PersistGeometry();
    }

    private void PersistGeometry()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top) ||
            !double.IsFinite(Width) || !double.IsFinite(Height)) return;
        _settings = (_settings with
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        }).Normalize();
        _persist(_settings);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _geometryTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        PersistGeometry();
    }
}
