using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SharpVectors.Converters;
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
using ProgressBar = System.Windows.Controls.ProgressBar;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SkyLyrics.Desktop;

public sealed class GameModeWindow : Window
{
    [Flags]
    private enum ResizeEdges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    private readonly ShellViewModel _viewModel;
    private readonly Action<GameModePreferences> _persist;
    private readonly Grid _root = new();
    private readonly Grid _header = new();
    private readonly Grid _footer = new();
    private readonly Border _surface = new();
    private readonly SvgViewbox _backgroundArt = new();
    private readonly ScaleTransform _backgroundScale = new(1.04d, 1.04d);
    private readonly TranslateTransform _backgroundTranslate = new();
    private readonly TextBlock _lyric = new();
    private readonly TextBlock _word = new();
    private readonly ProgressBar _wordProgress = new();
    private readonly ScaleTransform _wordScale = new(1d, 1d);
    private readonly TextBlock _meta = new();
    private readonly TextBlock _status = new();
    private readonly Button _pinButton = new();
    private readonly Button _clickThroughButton = new();
    private readonly ScaleTransform _scale = new(1d, 1d);
    private readonly TranslateTransform _translate = new();
    private readonly DispatcherTimer _geometryTimer;
    private GameModePreferences _settings;
    private string _lastLyric = string.Empty;
    private string _lastWord = string.Empty;
    private bool _applyingSettings;
    private bool _compact;
    private bool _ambientMotionStarted;

    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;

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
        MinWidth = 160;
        MinHeight = 30;
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
        SizeChanged += (_, _) =>
        {
            UpdateResponsiveLayout();
            ScheduleGeometrySave();
        };
        Loaded += (_, _) =>
        {
            UpdateResponsiveLayout();
            StartAmbientMotion();
        };
        Closed += OnClosed;
        SourceInitialized += (_, _) => ApplyClickThrough();

        BuildVisualTree();
        ApplySettings(_settings);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateScene();
    }

    private void BuildVisualTree()
    {
        _root.Margin = new Thickness(8);
        _surface.CornerRadius = new CornerRadius(20);
        _surface.BorderThickness = new Thickness(1.2);
        _surface.BorderBrush = MakeGradient("#A8E6CF", "#526D62");
        _surface.Background = BackgroundBrush(_settings.BackgroundId);
        _surface.Padding = new Thickness(22, 14, 22, 14);
        _surface.ClipToBounds = true;
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

        _header.Margin = new Thickness(0, 0, 0, 5);
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dragSurface = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 1, 12, 5),
            Cursor = Cursors.SizeAll
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = Brush("#A8E6CF"),
            Margin = new Thickness(0, 0, 9, 0)
        });
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
        _header.Children.Add(dragSurface);

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
        _clickThroughButton.Click += (_, _) => ToggleClickThrough();
        _clickThroughButton.ToolTip = "Toggle click-through; use Ctrl+Alt+Shift+G to restore interaction";
        StyleHeaderButton(_clickThroughButton, "#253833", "#637B70");
        var settingsButton = CreateHeaderButton("⚙", "Game Mode settings");
        settingsButton.Click += (_, _) => OpenSettings();
        var closeButton = CreateHeaderButton("×", "Close Game Mode");
        closeButton.Click += (_, _) => Close();
        actions.Children.Add(_pinButton);
        actions.Children.Add(_clickThroughButton);
        actions.Children.Add(settingsButton);
        actions.Children.Add(closeButton);
        Grid.SetColumn(actions, 1);
        _header.Children.Add(actions);
        Grid.SetRow(_header, 0);
        layout.Children.Add(_header);

        var lyricPanel = new Grid { Margin = new Thickness(10, 5, 10, 7), ClipToBounds = true };
        var lyricStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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
        lyricStack.Children.Add(_lyric);

        _word.TextAlignment = TextAlignment.Center;
        _word.FontSize = 12;
        _word.FontWeight = FontWeights.Bold;
        _word.Foreground = Brush("#A8E6CF");
        _word.Margin = new Thickness(0, 5, 0, 0);
        _word.RenderTransformOrigin = new Point(0.5d, 0.5d);
        _word.RenderTransform = _wordScale;
        _word.Visibility = Visibility.Collapsed;
        lyricStack.Children.Add(_word);

        _wordProgress.Minimum = 0d;
        _wordProgress.Maximum = 100d;
        _wordProgress.Height = 2d;
        _wordProgress.Margin = new Thickness(34, 4, 34, 0);
        _wordProgress.BorderThickness = new Thickness(0);
        _wordProgress.Background = Brush("#34403D");
        _wordProgress.Foreground = Brush("#A8E6CF");
        _wordProgress.Visibility = Visibility.Collapsed;
        lyricStack.Children.Add(_wordProgress);

        lyricPanel.Children.Add(lyricStack);
        lyricPanel.MouseLeftButtonDown += (_, e) =>
        {
            if (_compact && e.ButtonState == MouseButtonState.Pressed)
                DragSurface_MouseLeftButtonDown(this, e);
        };
        Grid.SetRow(lyricPanel, 1);
        layout.Children.Add(lyricPanel);

        _footer.Margin = new Thickness(1, 3, 1, 0);
        _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _meta.FontSize = 9;
        _meta.Foreground = Brush("#BCC7C3");
        _meta.TextTrimming = TextTrimming.CharacterEllipsis;
        _meta.VerticalAlignment = VerticalAlignment.Center;
        _footer.Children.Add(_meta);
        _status.FontSize = 9;
        _status.Foreground = Brush("#A8E6CF");
        _status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_status, 1);
        _footer.Children.Add(_status);
        Grid.SetRow(_footer, 2);
        layout.Children.Add(_footer);

        var backgroundTransforms = new TransformGroup();
        backgroundTransforms.Children.Add(_backgroundScale);
        backgroundTransforms.Children.Add(_backgroundTranslate);
        _backgroundArt.Source = BackgroundSource(_settings.BackgroundId);
        _backgroundArt.Stretch = Stretch.UniformToFill;
        _backgroundArt.RenderTransformOrigin = new Point(0.5d, 0.5d);
        _backgroundArt.RenderTransform = backgroundTransforms;
        _backgroundArt.IsHitTestVisible = false;
        _backgroundArt.Opacity = 0.86d;

        var scrimBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        scrimBrush.GradientStops.Add(new GradientStop(ColorFrom("#AE0B1014"), 0d));
        scrimBrush.GradientStops.Add(new GradientStop(ColorFrom("#C90B1014"), 0.52d));
        scrimBrush.GradientStops.Add(new GradientStop(ColorFrom("#D90B1014"), 1d));
        var scrim = new Border { Background = scrimBrush, IsHitTestVisible = false };

        var surfaceContent = new Grid();
        surfaceContent.Children.Add(_backgroundArt);
        surfaceContent.Children.Add(scrim);
        surfaceContent.Children.Add(layout);
        _surface.Child = surfaceContent;
        _root.Children.Add(_surface);

        AddResizeHandle(_root, ResizeEdges.Left, 8, null, HorizontalAlignment.Left, VerticalAlignment.Stretch, new Thickness(0, 4, 0, 4), Cursors.SizeWE);
        AddResizeHandle(_root, ResizeEdges.Right, 8, null, HorizontalAlignment.Right, VerticalAlignment.Stretch, new Thickness(0, 4, 0, 4), Cursors.SizeWE);
        AddResizeHandle(_root, ResizeEdges.Top, null, 8, HorizontalAlignment.Stretch, VerticalAlignment.Top, new Thickness(4, 0, 4, 0), Cursors.SizeNS);
        AddResizeHandle(_root, ResizeEdges.Bottom, null, 8, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, new Thickness(4, 0, 4, 0), Cursors.SizeNS);
        AddResizeHandle(_root, ResizeEdges.Left | ResizeEdges.Top, 12, 12, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(0), Cursors.SizeNWSE);
        AddResizeHandle(_root, ResizeEdges.Right | ResizeEdges.Top, 12, 12, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0), Cursors.SizeNESW);
        AddResizeHandle(_root, ResizeEdges.Left | ResizeEdges.Bottom, 12, 12, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(0), Cursors.SizeNESW);
        AddResizeHandle(_root, ResizeEdges.Right | ResizeEdges.Bottom, 12, 12, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0), Cursors.SizeNWSE);

        var menu = new ContextMenu();
        var menuSettings = new MenuItem { Header = "Game Mode settings" };
        menuSettings.Click += (_, _) => OpenSettings();
        var menuRestore = new MenuItem { Header = "Restore default size · 1000 × 176" };
        menuRestore.Click += (_, _) => UpdateSettings(_settings with { Width = 1000d, Height = 176d });
        var menuClose = new MenuItem { Header = "Close Game Mode" };
        menuClose.Click += (_, _) => Close();
        menu.Items.Add(menuSettings);
        menu.Items.Add(menuRestore);
        menu.Items.Add(new Separator());
        menu.Items.Add(menuClose);
        ContextMenu = menu;

        Content = _root;
    }

    private static Uri BackgroundSource(string id)
    {
        var asset = id switch
        {
            "ocean" => "ocean",
            "violet" => "violet",
            "sunset" => "sunset",
            "graphite" => "graphite",
            _ => "aurora"
        };
        return new Uri($"/SkyLyrics.Desktop;component/Assets/Backgrounds/{asset}.svg", UriKind.Relative);
    }

    private void UpdateResponsiveLayout()
    {
        var micro = Height <= 40d;
        _compact = micro || Height < 88d || Width < 360d;

        _root.Margin = new Thickness(micro ? 1d : _compact ? 4d : 8d);
        _surface.Padding = micro
            ? new Thickness(4d, 1d, 4d, 1d)
            : _compact ? new Thickness(10d, 5d, 10d, 5d) : new Thickness(22d, 14d, 22d, 14d);
        _surface.CornerRadius = new CornerRadius(micro ? 7d : _compact ? 13d : 20d);
        _surface.Effect = micro ? null : new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = _compact ? 10d : 22d,
            ShadowDepth = _compact ? 2d : 4d,
            Opacity = 0.42d,
            RenderingBias = RenderingBias.Performance
        };

        _header.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
        _footer.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
        _lyric.FontSize = micro
            ? Math.Clamp(Height * 0.36d, 9d, 13d)
            : _compact ? Math.Clamp(Height * 0.30d, 12d, 21d) : Math.Clamp(Height * 0.205d, 18d, 48d);
        _lyric.TextWrapping = _compact ? TextWrapping.NoWrap : TextWrapping.Wrap;
        _lyric.MaxHeight = Math.Max(18d, Height - (micro ? 5d : _compact ? 18d : 52d));
        _lyric.HorizontalAlignment = HorizontalAlignment.Stretch;
        _lyric.VerticalAlignment = VerticalAlignment.Center;
        _lyric.Margin = _compact ? new Thickness(2d, 0d, 2d, 0d) : new Thickness(0d);
        _backgroundArt.Opacity = micro ? 0.08d : _compact ? 0.42d : 0.86d;

        var showWord = !_compact && _viewModel.HasActiveWord;
        _word.Visibility = showWord ? Visibility.Visible : Visibility.Collapsed;
        _wordProgress.Visibility = showWord && _viewModel.HasTimedActiveWord ? Visibility.Visible : Visibility.Collapsed;
        if (showWord) _wordProgress.Value = Math.Clamp(_viewModel.ActiveWordProgress, 0d, 1d) * 100d;

        if (!micro && IsLoaded) StartAmbientMotion();
    }

    private void StartAmbientMotion()
    {
        if (_ambientMotionStarted || !IsLoaded || Height < 90d || !SystemParameters.ClientAreaAnimation)
            return;

        _ambientMotionStarted = true;
        var drift = new DoubleAnimation(-12d, 12d, TimeSpan.FromSeconds(17d))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var scale = new DoubleAnimation(1.02d, 1.075d, TimeSpan.FromSeconds(21d))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _backgroundTranslate.BeginAnimation(TranslateTransform.XProperty, drift);
        _backgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        _backgroundScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
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

    public void ToggleClickThrough() =>
        UpdateSettings(_settings with { ClickThrough = !_settings.ClickThrough });

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
        _clickThroughButton.Content = _settings.ClickThrough ? "CLICK-THROUGH ON" : "INTERACTIVE";
        _clickThroughButton.BorderBrush = Brush(_settings.ClickThrough ? "#A8E6CF" : "#637B70");
        _surface.Background = BackgroundBrush(_settings.BackgroundId);
        _backgroundArt.Source = BackgroundSource(_settings.BackgroundId);
        _lyric.Effect = TextEffect(_settings.TextEffectId);
        _applyingSettings = false;
        UpdateResponsiveLayout();
        ApplyClickThrough();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ActiveLyricText)
            or nameof(ShellViewModel.StageEmptyStateText)
            or nameof(ShellViewModel.SyncPositionLabel)
            or nameof(ShellViewModel.SyncStatusLabel)
            or nameof(ShellViewModel.StatusTitle)
            or nameof(ShellViewModel.ArtistLine)
            or nameof(ShellViewModel.ActiveWordText)
            or nameof(ShellViewModel.ActiveWordProgress)
            or nameof(ShellViewModel.HasTimedActiveWord))
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
        _status.Text = $"{_viewModel.SyncStatusLabel}  ·  {_viewModel.SyncPositionLabel}" +
            (_settings.ClickThrough ? "  ·  CLICK-THROUGH ON · CTRL+ALT+SHIFT+G TO RESTORE" : string.Empty);

        UpdateWord();
        if (string.Equals(next, _lastLyric, StringComparison.Ordinal)) return;
        _lastLyric = next;
        _lyric.Text = next;
        UpdateResponsiveLayout();
        AnimateLyric();
    }

    private void UpdateWord()
    {
        var next = _viewModel.ActiveWordText;
        if (!string.Equals(next, _lastWord, StringComparison.Ordinal))
        {
            _lastWord = next;
            _word.Text = next;
            if (!string.IsNullOrWhiteSpace(next) && !_compact) AnimateWord();
        }

        var visible = !_compact && !string.IsNullOrWhiteSpace(next);
        _word.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _wordProgress.Visibility = visible && _viewModel.HasTimedActiveWord ? Visibility.Visible : Visibility.Collapsed;
        if (visible && _viewModel.HasTimedActiveWord)
            _wordProgress.Value = Math.Clamp(_viewModel.ActiveWordProgress, 0d, 1d) * 100d;
    }

    private void AnimateWord()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            _word.Opacity = 1d;
            _wordScale.ScaleX = 1d;
            _wordScale.ScaleY = 1d;
            return;
        }

        _word.Opacity = 0.20d;
        _wordScale.ScaleX = 0.84d;
        _wordScale.ScaleY = 0.84d;
        var reveal = new DoubleAnimation(0.20d, 1d, TimeSpan.FromMilliseconds(190d))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var pop = new DoubleAnimation(0.84d, 1d, TimeSpan.FromMilliseconds(230d))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18d }
        };
        _word.BeginAnimation(OpacityProperty, reveal);
        _wordScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        _wordScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void AnimateLyric()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            _lyric.BeginAnimation(OpacityProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _translate.BeginAnimation(TranslateTransform.YProperty, null);
            _lyric.Opacity = 1d;
            _scale.ScaleX = 1d;
            _scale.ScaleY = 1d;
            _translate.Y = 0d;
            return;
        }

        var duration = _compact ? 160 : _settings.AnimationId switch
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

    private void ApplyClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var currentStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        var updatedStyle = _settings.ClickThrough
            ? currentStyle | WsExTransparent
            : currentStyle & ~WsExTransparent;

        if (updatedStyle != currentStyle)
        {
            SetWindowLongPtr(handle, GwlExStyle, new IntPtr(updatedStyle));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr newValue);

    private void OnClosed(object? sender, EventArgs e)
    {
        _geometryTimer.Stop();
        _backgroundTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        _backgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _backgroundScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        PersistGeometry();
    }
}
