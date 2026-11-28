using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SkyLyrics.Application.Startup;

namespace SkyLyrics.Desktop;

public partial class SplashWindow : Window
{
    public bool WasSkipped { get; private set; }

    private readonly SplashDismissalState _dismissal = new();
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;
    private bool _motionEnabled = true;

    public bool IsDismissed => _dismissal.IsDismissed;

    public void SetMotionEnabled(bool enabled)
    {
        _motionEnabled = enabled;
    }

    public SplashWindow()
    {
        InitializeComponent();
    }

    public void SetProgress(double value, string stage)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetProgress(value, stage)));
            return;
        }

        if (!IsVisible || IsDismissed)
        {
            return;
        }

        var normalized = Math.Clamp(value, 0d, 100d);
        StartupProgress.Value = normalized;
        ProgressText.Text = $"{normalized:0}%";
        StageText.Text = stage.ToUpperInvariant();
    }

    public void Finish()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Finish));
            return;
        }

        if (!_dismissal.TryDismiss())
        {
            StopAutoCloseTimer();
            if (IsVisible)
            {
                Close();
            }
            return;
        }

        StopAutoCloseTimer();
        if (!IsVisible || !_motionEnabled)
        {
            Close();
            return;
        }

        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            });

        var closeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            Close();
        };
        closeTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2200d)
        };
        _autoCloseTimer.Tick += (_, _) => Finish();
        _autoCloseTimer.Start();

        if (!_motionEnabled)
        {
            BrandLogo.Opacity = 1d;
            BrandTitle.Opacity = 1d;
            if (BrandLogo.RenderTransform is ScaleTransform staticLogoScale)
            {
                staticLogoScale.ScaleX = 1d;
                staticLogoScale.ScaleY = 1d;
            }

            if (BrandTitle.RenderTransform is ScaleTransform staticTitleScale)
            {
                staticTitleScale.ScaleX = 1d;
            }

            return;
        }

        var easing = new QuinticEase { EasingMode = EasingMode.EaseOut };
        BrandLogo.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(620)) { EasingFunction = easing });
        BrandTitle.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(720))
            {
                BeginTime = TimeSpan.FromMilliseconds(110),
                EasingFunction = easing
            });

        if (BrandLogo.RenderTransform is ScaleTransform logoScale)
        {
            logoScale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.86d, 1d, TimeSpan.FromMilliseconds(620)) { EasingFunction = easing });
            logoScale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.86d, 1d, TimeSpan.FromMilliseconds(620)) { EasingFunction = easing });
        }

        if (BrandTitle.RenderTransform is ScaleTransform titleScale)
        {
            titleScale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.94d, 1d, TimeSpan.FromMilliseconds(720))
                {
                    BeginTime = TimeSpan.FromMilliseconds(110),
                    EasingFunction = easing
                });
        }

        if (SplashBackground.RenderTransform is ScaleTransform backgroundScale)
        {
            backgroundScale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.02d, 1.12d, TimeSpan.FromSeconds(18))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            backgroundScale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.02d, 1.12d, TimeSpan.FromSeconds(18))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Skip();
        e.Handled = true;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        Skip();
        e.Handled = true;
    }

    private void StopAutoCloseTimer()
    {
        if (_autoCloseTimer is null)
        {
            return;
        }

        _autoCloseTimer.Stop();
        _autoCloseTimer = null;
    }

    private void Skip()
    {
        StopAutoCloseTimer();
        WasSkipped = true;
        _dismissal.TryDismiss();
        Close();
    }
}
