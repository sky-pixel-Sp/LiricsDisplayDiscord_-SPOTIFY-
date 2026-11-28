using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SkyLyrics.Application.Settings;

namespace SkyLyrics.Desktop.Controls;

public sealed class MusicWaveformControl : FrameworkElement
{
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(TimeSpan), typeof(MusicWaveformControl),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualInputChanged));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(MusicWaveformControl),
        new FrameworkPropertyMetadata(false, OnVisualInputChanged));

    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(MusicWaveformControl),
        new FrameworkPropertyMetadata(true, OnVisualInputChanged));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(MusicWaveformControl),
        new FrameworkPropertyMetadata(Color.FromRgb(118, 202, 176), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty QualityTierProperty = DependencyProperty.Register(
        nameof(QualityTier), typeof(string), typeof(MusicWaveformControl),
        new FrameworkPropertyMetadata("balanced", OnVisualInputChanged));

    private readonly Stopwatch _playbackElapsed = new();
    private readonly Stopwatch _frameClock = new();
    private AdaptiveQualityController _adaptiveQuality = new();
    private CinematicQualityLevel _effectiveQualityLevel = CinematicQualityLevel.Balanced;
    private bool _renderSubscribed;
    private TimeSpan _lastPosition;
    private bool _hasLastPosition;

    public event Action<CinematicQualityLevel>? EffectiveQualityChanged;

    public MusicWaveformControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateRenderingSubscription();
    }

    public TimeSpan Position
    {
        get => (TimeSpan)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool MotionEnabled
    {
        get => (bool)GetValue(MotionEnabledProperty);
        set => SetValue(MotionEnabledProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public string QualityTier
    {
        get => (string)GetValue(QualityTierProperty);
        set => SetValue(QualityTierProperty, value);
    }

    private static void OnVisualInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (MusicWaveformControl)dependencyObject;
        if (e.Property == QualityTierProperty)
        {
            control.ResetAdaptiveQuality();
        }
        else if (e.Property == PositionProperty)
        {
            control.ResetPlaybackPhaseIfSeek((TimeSpan)e.NewValue);
        }

        control.UpdateRenderingSubscription();
        control.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _playbackElapsed.Reset();
        _frameClock.Restart();
        UpdateRenderingSubscription();
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_renderSubscribed)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderSubscribed = false;
        }

        _playbackElapsed.Stop();
        _frameClock.Stop();
    }

    private void UpdateRenderingSubscription()
    {
        var shouldAnimate = IsLoaded && IsVisible && MotionEnabled && IsPlaying &&
                            !StringComparer.OrdinalIgnoreCase.Equals(QualityTier, "reduced") &&
                            _effectiveQualityLevel != CinematicQualityLevel.Reduced;

        if (shouldAnimate && !_renderSubscribed)
        {
            _playbackElapsed.Start();
            _frameClock.Restart();
            CompositionTarget.Rendering += OnRendering;
            _renderSubscribed = true;
        }
        else if (!shouldAnimate && _renderSubscribed)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderSubscribed = false;
            _playbackElapsed.Stop();
            _frameClock.Stop();
            InvalidateVisual();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var frameTime = _frameClock.Elapsed;
        _frameClock.Restart();

        if (_adaptiveQuality.ObserveFrame(frameTime))
        {
            _effectiveQualityLevel = _adaptiveQuality.Level;
            EffectiveQualityChanged?.Invoke(_effectiveQualityLevel);
            UpdateRenderingSubscription();
        }

        InvalidateVisual();
    }

    private void ResetAdaptiveQuality()
    {
        _effectiveQualityLevel = QualityTier.ToLowerInvariant() switch
        {
            "cinematic" => CinematicQualityLevel.Cinematic,
            "lite" => CinematicQualityLevel.Lite,
            "reduced" => CinematicQualityLevel.Reduced,
            _ => CinematicQualityLevel.Balanced
        };
        _adaptiveQuality = new AdaptiveQualityController(_effectiveQualityLevel);
        EffectiveQualityChanged?.Invoke(_effectiveQualityLevel);
    }

    private void ResetPlaybackPhaseIfSeek(TimeSpan newPosition)
    {
        if (_hasLastPosition && (newPosition - _lastPosition).Duration() > TimeSpan.FromSeconds(1.5d))
        {
            _playbackElapsed.Restart();
        }

        _lastPosition = newPosition;
        _hasLastPosition = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1d || height <= 1d)
        {
            return;
        }

        var quality = _effectiveQualityLevel;
        var waveCount = quality switch
        {
            CinematicQualityLevel.Cinematic => 4,
            CinematicQualityLevel.Lite => 2,
            CinematicQualityLevel.Reduced => 1,
            _ => 3
        };
        var sampleCount = quality switch
        {
            CinematicQualityLevel.Cinematic => 220,
            CinematicQualityLevel.Lite => 72,
            CinematicQualityLevel.Reduced => 40,
            _ => 140
        };

        var motionSeconds = IsPlaying && MotionEnabled ? _playbackElapsed.Elapsed.TotalSeconds : 0d;
        var mediaSeconds = Math.Max(0d, Position.TotalSeconds) + motionSeconds;
        var color = AccentColor;

        for (var wave = 0; wave < waveCount; wave++)
        {
            var alpha = (byte)Math.Clamp(88 - wave * 14, 24, 96);
            var pen = new Pen(
                new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
                wave == 0 ? 2.0d : 1.15d);
            pen.Freeze();

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var center = height * (0.42d + wave * 0.055d);
                var amplitude = height * (0.16d - wave * 0.018d);
                var frequency = 1.7d + wave * 0.44d;
                var phase = mediaSeconds * (0.55d + wave * 0.08d) + wave * 1.31d;

                for (var sample = 0; sample <= sampleCount; sample++)
                {
                    var x = width * sample / sampleCount;
                    var normalized = sample / (double)sampleCount;
                    var envelope = 0.32d + 0.68d * Math.Sin(Math.PI * normalized);
                    var y = center +
                            Math.Sin((normalized * Math.PI * 2d * frequency) + phase) * amplitude * envelope +
                            Math.Sin((normalized * Math.PI * 4.3d) - phase * 0.43d) * amplitude * 0.18d;

                    var point = new Point(x, y);
                    if (sample == 0)
                    {
                        context.BeginFigure(point, false, false);
                    }
                    else
                    {
                        context.LineTo(point, true, false);
                    }
                }
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }
    }
}
