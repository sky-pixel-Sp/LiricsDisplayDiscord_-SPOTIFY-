using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Outputs;
using SkyLyrics.Application.Playback;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Infrastructure.Windows.Playback;

namespace SkyLyrics.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<SpotifyAppIdentityMatcher>();
            builder.Services.AddSingleton<SpotifyGsmTcSessionMonitor>();
            builder.Services.AddSingleton<IPlaybackSessionSource>(
                services => services.GetRequiredService<SpotifyGsmTcSessionMonitor>());
            builder.Services.AddSingleton<IHostedService>(
                services => services.GetRequiredService<SpotifyGsmTcSessionMonitor>());

            builder.Services.AddSingleton<SceneStateRouter>();
            builder.Services.AddSingleton<ShellViewModel>();
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            MessageBox.Show(
                $"SkyLyrics Desktop could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "SkyLyrics Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _host.StopAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _host.Dispose();
                _host = null;
            }
        }

        base.OnExit(e);
    }
}
