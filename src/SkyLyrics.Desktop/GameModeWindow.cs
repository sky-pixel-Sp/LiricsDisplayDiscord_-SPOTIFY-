using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;

namespace SkyLyrics.Desktop;

public sealed class GameModeWindow : Window
{
    private readonly Border _surface;
    private readonly TextBlock _lyric;
    private string _lastText = string.Empty;
    private string _animationId;

    public GameModeWindow(AppearanceSettings settings)
    {
        _animationId = settings.AnimationId;
        Title = "SkyLyrics Game Mode";
        Width = 920;
        Height = 78;
        MinWidth = 360;
        MaxWidth = 1400;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _lyric = new TextBlock
        {
            Text = "Waiting for synchronized lyrics",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F5FFF9"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Effect = AppearanceCatalog.CreateTextEffect(settings.EffectId),
            Margin = new Thickness(20, 0, 20, 0)
        };

        _surface = new Border
        {
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("#759789"),
            Background = AppearanceCatalog.CreateBackgroundBrush(settings.BackgroundId),
            Padding = new Thickness(14, 8, 14, 8),
            Child = _lyric,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 16,
                ShadowDepth = 4,
                Opacity = .48,
                RenderingBias = RenderingBias.Performance
            }
        };

        Content = _surface;
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch (InvalidOperationException) { }
            }
        };
        MouseRightButtonUp += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        Loaded += (_, _) => PositionAtTop();
    }

    public void ApplyAppearance(AppearanceSettings settings)
    {
        _animationId = settings.AnimationId;
        _surface.Background = AppearanceCatalog.CreateBackgroundBrush(settings.BackgroundId);
        _lyric.Effect = AppearanceCatalog.CreateTextEffect(settings.EffectId);
    }

    public void UpdateLyric(string? text)
    {
        var next = string.IsNullOrWhiteSpace(text)
            ? "Synchronized lyrics are not available"
            : string.Join(' ', text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (string.Equals(next, _lastText, StringComparison.Ordinal))
            return;

        _lastText = next;
        _lyric.Text = next;
        var preset = AppearanceCatalog.GetAnimation(_animationId);
        var duration = new Duration(TimeSpan.FromMilliseconds(preset.DurationMs));
        var easing = AppearanceCatalog.CreateEasing(preset.Easing);
        var transform = new TranslateTransform(preset.FromX, preset.FromY);
        _lyric.RenderTransform = transform;
        _lyric.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
        transform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(preset.FromX, 0, duration) { EasingFunction = easing });
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(preset.FromY, 0, duration) { EasingFunction = easing });
    }

    private void PositionAtTop()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + 12;
    }

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value));
}
