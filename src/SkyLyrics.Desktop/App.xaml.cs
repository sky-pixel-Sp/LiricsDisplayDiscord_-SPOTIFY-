using System.Net.Http;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Application.Outputs;
using SkyLyrics.Application.Playback;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Infrastructure.Lyrics;
using SkyLyrics.Infrastructure.Persistence;
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

            builder.Services.AddSingleton<SqliteLyricsCache>();
            builder.Services.AddSingleton<ILyricsCache>(
                services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<IHostedService>(
                services => services.GetRequiredService<SqliteLyricsCache>());

            builder.Services.AddSingleton<LrcParser>();
            builder.Services.AddSingleton<LyricsFileParser>();
            builder.Services.AddSingleton<LocalLyricsLoader>();

            builder.Services.AddSingleton<HttpClient>(_ =>
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("https://lrclib.net/"),
                    Timeout = TimeSpan.FromSeconds(15)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "SkyLyricsDesktop/0.2.0 (https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-)");
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                return client;
            });

            builder.Services.AddSingleton<ILyricsProvider, LrclibClient>();
            builder.Services.AddSingleton<ILyricsService, LyricsService>();

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
