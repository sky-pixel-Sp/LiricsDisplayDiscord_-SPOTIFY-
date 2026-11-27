using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Application.Settings;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Application.Outputs;

public sealed class SceneStateOutputBridge : IHostedService
{
    private readonly SyncScheduler _scheduler;
    private readonly SceneStateRouter _router;
    private readonly IAppPreferencesStore _preferences;
    private readonly ILogger<SceneStateOutputBridge> _logger;
    private readonly Channel<SceneState> _queue = Channel.CreateBounded<SceneState>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _worker;

    public SceneStateOutputBridge(
        SyncScheduler scheduler,
        SceneStateRouter router,
        IAppPreferencesStore preferences,
        ILogger<SceneStateOutputBridge> logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler.SceneChanged += OnSceneChanged;
        _preferences.PreferencesChanged += OnPreferencesChanged;
        _worker = Task.Run(() => ProcessQueueAsync(_shutdown.Token), CancellationToken.None);
        _queue.Writer.TryWrite(_scheduler.Current);
        return Task.CompletedTask;
    }

    private void OnSceneChanged(object? sender, SceneStateChangedEventArgs e) =>
        _queue.Writer.TryWrite(e.State);

    private void OnPreferencesChanged(AppPreferences preferences) =>
        _queue.Writer.TryWrite(_scheduler.Current);

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var scene in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await _router.PublishAsync(scene, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Scene output bridge stopped unexpectedly.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _scheduler.SceneChanged -= OnSceneChanged;
        _preferences.PreferencesChanged -= OnPreferencesChanged;
        _queue.Writer.TryComplete();
        _shutdown.Cancel();

        if (_worker is not null)
        {
            try
            {
                await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scene output bridge shutdown exceeded the host stop timeout.");
            }
        }

        _shutdown.Dispose();
    }
}
