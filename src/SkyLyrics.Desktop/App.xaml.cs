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
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Desktop.Outputs;
using SkyLyrics.Desktop.Services;
using SkyLyrics.Desktop.ViewModels;
using SkyLyrics.Infrastructure.Lyrics;
using SkyLyrics.Infrastructure.Outputs;
using SkyLyrics.Infrastructure.Persistence;
using SkyLyrics.Infrastructure.Windows.Playback;

namespace SkyLyrics.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private SystemTrayService? _tray;
    private GlobalHotkeyService? _hotkeys;

    protected override async void OnStartup(StartupEventArgs e)
    {
        System.Windows.Forms.Application.SetHighDpiMode(
            System.Windows.Forms.HighDpiMode.PerMonitorV2);
        base.OnStartup(e);
        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<AppPreferencesStore>();
            builder.Services.AddSingleton<IAppPreferencesStore>(services => services.GetRequiredService<AppPreferencesStore>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<AppPreferencesStore>());

            builder.Services.AddSingleton<SqliteLyricsCache>();
            builder.Services.AddSingleton<ILyricsCache>(services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<ILyricsLibrary>(services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<SqliteLyricsCache>());
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
                    "SkyLyricsDesktop/2.1.0-preview.1 (https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-)");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return client;
            });

            builder.Services.AddSingleton<ILyricsProvider, LrclibClient>();
            builder.Services.AddSingleton<ILyricsService, LyricsService>();
            builder.Services.AddSingleton<WindowsMediaSessionMonitor>();
            builder.Services.AddSingleton<IPlaybackSessionSource>(services => services.GetRequiredService<WindowsMediaSessionMonitor>());
            builder.Services.AddSingleton<ISelectablePlaybackSessionSource>(services => services.GetRequiredService<WindowsMediaSessionMonitor>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<WindowsMediaSessionMonitor>());

            builder.Services.AddSingleton<SyncScheduler>();
            builder.Services.AddSingleton<ShellViewModel>();
            builder.Services.AddSingleton<WindowModeService>();
            builder.Services.AddSingleton<MainWindow>();

            builder.Services.AddSingleton<DiscordPresenceSink>();
            builder.Services.AddSingleton<IOutputSink>(services => services.GetRequiredService<DiscordPresenceSink>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<DiscordPresenceSink>());
            builder.Services.AddSingleton<ObsOverlayServerSink>();
            builder.Services.AddSingleton<IOutputSink>(services => services.GetRequiredService<ObsOverlayServerSink>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<ObsOverlayServerSink>());
            builder.Services.AddSingleton<SceneStateRouter>();
            builder.Services.AddSingleton<SceneStateOutputBridge>();
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<SceneStateOutputBridge>());

            _host = builder.Build();
            await _host.StartAsync();

            var preferences = _host.Services.GetRequiredService<IAppPreferencesStore>();
            ThemeManager.Apply(preferences.Current.Theme);
            var activeProfile = preferences.Current.Customization.Profiles
                .First(profile => string.Equals(
                    profile.Id,
                    preferences.Current.Customization.ActiveProfileId,
                    StringComparison.OrdinalIgnoreCase));
            ThemeManager.ApplyProfile(activeProfile);
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var windowModes = _host.Services.GetRequiredService<WindowModeService>();
            MainWindow = mainWindow;
            _hotkeys = new GlobalHotkeyService(mainWindow, windowModes);
            mainWindow.Show();
            _tray = new SystemTrayService(
                mainWindow.ShowFromTray,
                windowModes.ShowFocusView,
                windowModes.ToggleMiniPlayer,
                windowModes.ToggleGameMode,
                mainWindow.ShowSettings,
                mainWindow.ShowDiagnostics,
                () => Dispatcher.BeginInvoke(new Action(mainWindow.RequestApplicationExit)));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            System.Windows.MessageBox.Show(
                $"SkyLyrics Desktop could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "SkyLyrics Desktop",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        _tray?.Dispose();
        _tray = null;

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
