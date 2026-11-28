using Microsoft.Extensions.Hosting;

namespace SkyLyrics.Desktop.Services;

public sealed class SplashProgressReporter(
    SplashWindow splash,
    Func<bool> isEnabled,
    double progress,
    string stage) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!isEnabled())
        {
            return Task.CompletedTask;
        }

        if (!splash.IsVisible)
        {
            splash.Show();
        }

        splash.SetProgress(progress, stage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
