using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SkyLyrics.Application.Outputs;
using SkyLyrics.Application.Settings;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Infrastructure.Outputs;

public sealed class DiscordPresenceSink : IOutputSink, IHostedService
{
    private readonly IAppPreferencesStore _preferences;
    private readonly ILogger<DiscordPresenceSink> _logger;
    private NamedPipeClientStream? _pipe;
    private string? _connectedApplicationId;
    private string? _lastActivitySignature;
    private DateTimeOffset _nextConnectionAttempt = DateTimeOffset.MinValue;
    private bool _disposed;

    public DiscordPresenceSink(
        IAppPreferencesStore preferences,
        ILogger<DiscordPresenceSink> logger)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _preferences.PreferencesChanged += OnPreferencesChanged;
        return Task.CompletedTask;
    }

    private async void OnPreferencesChanged(AppPreferences preferences)
    {
        if (preferences.DiscordPresenceEnabled &&
            AppPreferences.IsValidDiscordApplicationId(preferences.DiscordApplicationId))
        {
            return;
        }

        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Discord presence could not be cleared after settings changed.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _preferences.PreferencesChanged -= OnPreferencesChanged;
        await DisconnectAsync().ConfigureAwait(false);
    }

    public string Name => "Discord Rich Presence";

    public bool IsEnabled =>
        _preferences.Current.DiscordPresenceEnabled &&
        AppPreferences.IsValidDiscordApplicationId(_preferences.Current.DiscordApplicationId);

    public async ValueTask PublishAsync(SceneState scene, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);

        var settings = _preferences.Current;
        var applicationId = settings.DiscordApplicationId;
        if (!settings.DiscordPresenceEnabled || !AppPreferences.IsValidDiscordApplicationId(applicationId))
        {
            await DisconnectAsync().ConfigureAwait(false);
            return;
        }

        if (!string.Equals(_connectedApplicationId, applicationId, StringComparison.Ordinal))
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        if (!await EnsureConnectedAsync(applicationId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var mapped = DiscordPresenceMapper.Map(scene, DateTimeOffset.UtcNow);
        var signature = mapped is null
            ? "<clear>"
            : $"{mapped.Details}\n{mapped.State}\n{mapped.Type}";
        if (StringComparer.Ordinal.Equals(signature, _lastActivitySignature))
        {
            return;
        }

        var command = new
        {
            cmd = "SET_ACTIVITY",
            args = new
            {
                pid = Environment.ProcessId,
                activity = mapped is null
                    ? null
                    : new
                    {
                        details = mapped.Details,
                        state = mapped.State,
                        type = mapped.Type,
                        timestamps = mapped.StartedAtUnixMilliseconds is { } start
                            ? new { start }
                            : null,
                        instance = false
                    }
            },
            nonce = Guid.NewGuid().ToString("N")
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        await DiscordIpcFrameCodec.WriteAsync(_pipe!, 1, bytes, cancellationToken).ConfigureAwait(false);
        _lastActivitySignature = signature;
    }

    private async Task<bool> EnsureConnectedAsync(string applicationId, CancellationToken cancellationToken)
    {
        if (_pipe?.IsConnected == true)
        {
            return true;
        }

        if (_pipe is not null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
            _connectedApplicationId = null;
            _lastActivitySignature = null;
        }

        if (DateTimeOffset.UtcNow < _nextConnectionAttempt)
        {
            return false;
        }

        _nextConnectionAttempt = DateTimeOffset.UtcNow.AddSeconds(5);
        for (var index = 0; index < 10; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = new NamedPipeClientStream(
                ".",
                $"discord-ipc-{index}",
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                await candidate.ConnectAsync(350, cancellationToken).ConfigureAwait(false);
                var handshake = JsonSerializer.SerializeToUtf8Bytes(new { v = 1, client_id = applicationId });
                await DiscordIpcFrameCodec.WriteAsync(candidate, 0, handshake, cancellationToken).ConfigureAwait(false);

                using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                var ready = await DiscordIpcFrameCodec.ReadAsync(candidate, handshakeTimeout.Token).ConfigureAwait(false);
                if (ready.Opcode != 1 ||
                    !System.Text.Encoding.UTF8.GetString(ready.Payload).Contains("\"READY\"", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Discord IPC did not return a READY event.");
                }

                _pipe = candidate;
                _connectedApplicationId = applicationId;
                _lastActivitySignature = null;
                _logger.LogInformation("Discord Rich Presence IPC connection established.");
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                candidate.Dispose();
            }
            catch (OperationCanceledException)
            {
                candidate.Dispose();
                throw;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or InvalidDataException)
            {
                candidate.Dispose();
            }
        }

        _logger.LogDebug("Discord desktop IPC is unavailable; Rich Presence will retry.");
        return false;
    }

    private async ValueTask DisconnectAsync()
    {
        if (_pipe is not null)
        {
            try
            {
                if (_pipe.IsConnected)
                {
                    var clear = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        cmd = "SET_ACTIVITY",
                        args = new { pid = Environment.ProcessId, activity = (object?)null },
                        nonce = Guid.NewGuid().ToString("N")
                    });
                    await DiscordIpcFrameCodec.WriteAsync(_pipe, 1, clear, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                _logger.LogDebug(exception, "Discord IPC connection closed while clearing presence.");
            }

            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }

        _connectedApplicationId = null;
        _lastActivitySignature = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
    }
}
