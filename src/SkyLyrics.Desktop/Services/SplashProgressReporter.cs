using System.Windows.Threading;
using Microsoft.Extensions.Hosting;

namespace SkyLyrics.Desktop.Services;

public sealed class SplashProgressReporter(
    SplashWindow splash,
    Func<bool> isEnabled,
    Func<bool> reduceMotion,
    double progress,
    string stage) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!isEnabled() || splash.IsDismissed)
        {
            return;
        }

        if (splash.Dispatcher.CheckAccess())
        {
            ShowProgress();
            return;
        }

        await splash.Dispatcher.InvokeAsync(ShowProgress, DispatcherPriority.Normal, cancellationToken).Task
            .ConfigureAwait(false);
    }

    private void ShowProgress()
    {
        if (splash.IsDismissed)
        {
            return;
        }

        splash.SetMotionEnabled(!reduceMotion());
        if (!splash.IsVisible)
        {
            splash.Show();
        }

        splash.SetProgress(progress, stage);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
