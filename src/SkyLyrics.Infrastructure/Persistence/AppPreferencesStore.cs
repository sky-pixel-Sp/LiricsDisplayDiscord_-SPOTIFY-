using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Settings;

namespace SkyLyrics.Infrastructure.Persistence;

public sealed class AppPreferencesStore : IAppPreferencesStore, IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly ILogger<AppPreferencesStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppPreferences _current = new();
    private bool _disposed;

    public AppPreferencesStore(ILogger<AppPreferencesStore> logger, string? settingsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _path = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyLyrics",
            "settings.json");
    }

    public AppPreferences Current => Volatile.Read(ref _current);

    public event Action<AppPreferences>? PreferencesChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            try
            {
                await using var stream = new FileStream(
                    _path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var loaded = await JsonSerializer.DeserializeAsync<AppPreferences>(
                    stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _current, (loaded ?? new AppPreferences()).Normalize());
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "SkyLyrics settings file is invalid; defaults are active.");
                Volatile.Write(ref _current, new AppPreferences());
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "SkyLyrics settings file could not be read; defaults are active.");
                Volatile.Write(ref _current, new AppPreferences());
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(
        AppPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalized = preferences.Normalize();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("Settings directory could not be determined.");
            Directory.CreateDirectory(directory);

            var temporaryPath = _path + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
            Volatile.Write(ref _current, normalized);
            var handlers = PreferencesChanged;
            if (handlers is not null)
            {
                foreach (Action<AppPreferences> handler in handlers.GetInvocationList())
                {
                    try { handler(normalized); }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "A preferences-change subscriber failed.");
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
