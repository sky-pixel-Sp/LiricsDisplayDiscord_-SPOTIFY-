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
    private SplashWindow? _splash;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _splash = new SplashWindow();
        _splash.Show();
        _splash.SetProgress(4d, "Starting SkyLyrics");
        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<AppPreferencesStore>();
            builder.Services.AddSingleton<IAppPreferencesStore>(services => services.GetRequiredService<AppPreferencesStore>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<AppPreferencesStore>());
            builder.Services.AddSingleton<IHostedService>(services => new SplashProgressReporter(
                _splash!,
                () => services.GetRequiredService<IAppPreferencesStore>().Current.SplashEnabled,
                18d,
                "Local settings ready"));

            builder.Services.AddSingleton<SqliteLyricsCache>();
            builder.Services.AddSingleton<ILyricsCache>(services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<ILyricsLibrary>(services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<SqliteLyricsCache>());
            builder.Services.AddSingleton<IHostedService>(services => new SplashProgressReporter(
                _splash!,
                () => services.GetRequiredService<IAppPreferencesStore>().Current.SplashEnabled,
                40d,
                "SQLite lyrics cache ready"));
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
            builder.Services.AddSingleton<IHostedService>(services => new SplashProgressReporter(
                _splash!,
                () => services.GetRequiredService<IAppPreferencesStore>().Current.SplashEnabled,
                66d,
                "Windows media session monitor ready"));

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
            _splash?.SetProgress(82d, "Application services ready");
            var syncScheduler = _host.Services.GetRequiredService<SyncScheduler>();
            syncScheduler.SetLineAnimationLeadTime(
                preferences.Current.ReduceMotion
                    ? TimeSpan.Zero
                    : TimeSpan.FromMilliseconds(preferences.Current.LyricAnimationLeadMilliseconds));
            ThemeManager.Apply(preferences.Current.Theme);
            var activeProfile = preferences.Current.Customization.Profiles
                .First(profile => string.Equals(
                    profile.Id,
                    preferences.Current.Customization.ActiveProfileId,
                    StringComparison.OrdinalIgnoreCase));
            ThemeManager.ApplyProfile(activeProfile);
            _splash?.SetProgress(94d, "Theme and visual profile ready");
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var windowModes = _host.Services.GetRequiredService<WindowModeService>();
            MainWindow = mainWindow;
            _hotkeys = new GlobalHotkeyService(mainWindow, windowModes);
            mainWindow.Show();
            _splash?.SetProgress(100d, "Ready");
            _splash?.Finish();
            _splash = null;
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
            _splash?.Close();
            _splash = null;
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
        if (_splash is { IsVisible: true })
        {
            _splash.Close();
        }
        _splash = null;
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
