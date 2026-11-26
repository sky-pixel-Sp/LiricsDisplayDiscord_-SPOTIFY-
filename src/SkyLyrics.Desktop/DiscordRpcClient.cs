using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Desktop;

/// <summary>
/// Minimal Discord Rich Presence client using the local Windows IPC pipe.
/// It never authenticates a Discord user, sends messages, joins servers, or reads voice/audio.
/// </summary>
public sealed class DiscordRpcClient : IAsyncDisposable
{
    private const int HandshakeOpcode = 0;
    private const int FrameOpcode = 1;
    private const int CloseOpcode = 2;
    private const int MaximumFrameLength = 1024 * 1024;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private string? _applicationId;
    private string? _lastActivityFingerprint;
    private bool _disposed;

    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task ConnectAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        ValidateApplicationId(applicationId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsConnected && string.Equals(_applicationId, applicationId, StringComparison.Ordinal))
                return;

            await ConnectUnsafeAsync(applicationId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActivityAsync(
        string applicationId,
        DiscordPresenceData? presence,
        CancellationToken cancellationToken = default)
    {
        ValidateApplicationId(applicationId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsConnected || !string.Equals(_applicationId, applicationId, StringComparison.Ordinal))
                await ConnectUnsafeAsync(applicationId, cancellationToken);

            var fingerprint = presence is null
                ? "<cleared>"
                : $"{presence.Details}\u001f{presence.State}\u001f{presence.IsPlaying}";

            if (string.Equals(_lastActivityFingerprint, fingerprint, StringComparison.Ordinal))
                return;

            await SendSetActivityUnsafeAsync(presence, cancellationToken);
            _lastActivityFingerprint = fingerprint;
        }
        catch
        {
            await ClosePipeUnsafeAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_pipe is { IsConnected: true })
            {
                try
                {
                    await SendSetActivityUnsafeAsync(null, cancellationToken);
                }
                catch
                {
                    // A closing Discord client may already have torn down its IPC pipe.
                }
            }

            await ClosePipeUnsafeAsync();
            _applicationId = null;
            _lastActivityFingerprint = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ConnectUnsafeAsync(string applicationId, CancellationToken cancellationToken)
    {
        await ClosePipeUnsafeAsync();

        Exception? lastError = null;

        for (var index = 0; index < 10; index++)
        {
            var candidate = new NamedPipeClientStream(
                ".",
                $"discord-ipc-{index}",
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    connectTimeout.CancelAfter(TimeSpan.FromMilliseconds(150));
                    await candidate.ConnectAsync(150, connectTimeout.Token);
                }

                var handshake = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    v = 1,
                    client_id = applicationId
                });

                await WriteFrameAsync(candidate, HandshakeOpcode, handshake, cancellationToken);

                IpcFrame handshakeResponse;
                using (var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    responseTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                    handshakeResponse = await ReadFrameAsync(candidate, responseTimeout.Token);
                }

                EnsureReady(handshakeResponse);
                _pipe = candidate;
                _applicationId = applicationId;
                _lastActivityFingerprint = null;
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                lastError = ex;
                await candidate.DisposeAsync();

                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        throw new IOException(
            "Discord Desktop IPC was not found. Open the Discord desktop app and try again.",
            lastError);
    }

    private async Task SendSetActivityUnsafeAsync(
        DiscordPresenceData? presence,
        CancellationToken cancellationToken)
    {
        var pipe = _pipe;
        if (pipe is null || !pipe.IsConnected)
            throw new IOException("The Discord IPC connection is no longer available.");

        object? activity = null;

        if (presence is not null)
        {
            object? timestamps = presence.StartTimestamp is null && presence.EndTimestamp is null
                ? null
                : new
                {
                    start = presence.StartTimestamp,
                    end = presence.EndTimestamp
                };

            activity = new
            {
                type = 2,
                details = presence.Details,
                state = presence.State,
                timestamps
            };
        }

        var nonce = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            cmd = "SET_ACTIVITY",
            args = new
            {
                pid = Environment.ProcessId,
                activity
            },
            nonce
        });

        await WriteFrameAsync(pipe, FrameOpcode, payload, cancellationToken);

        using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseTimeout.CancelAfter(TimeSpan.FromSeconds(3));

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var response = await ReadFrameAsync(pipe, responseTimeout.Token);

            if (response.Opcode == CloseOpcode)
                throw new IOException("Discord closed the Rich Presence connection.");

            if (response.Opcode != FrameOpcode)
                continue;

            using var document = JsonDocument.Parse(response.Payload);
            var root = document.RootElement;

            if (root.TryGetProperty("evt", out var eventElement) &&
                string.Equals(eventElement.GetString(), "ERROR", StringComparison.Ordinal))
            {
                var message = root.TryGetProperty("data", out var data) &&
                              data.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;

                throw new IOException(
                    string.IsNullOrWhiteSpace(message)
                        ? "Discord rejected the Rich Presence update."
                        : $"Discord rejected the Rich Presence update: {message}");
            }

            if (root.TryGetProperty("nonce", out var nonceElement) &&
                string.Equals(nonceElement.GetString(), nonce, StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new IOException("Discord did not acknowledge the Rich Presence update.");
    }

    private static void EnsureReady(IpcFrame frame)
    {
        if (frame.Opcode == CloseOpcode)
            throw new IOException("Discord closed the IPC handshake.");

        if (frame.Opcode != FrameOpcode)
            throw new IOException("Discord returned an unexpected IPC handshake frame.");

        using var document = JsonDocument.Parse(frame.Payload);
        var root = document.RootElement;

        if (root.TryGetProperty("evt", out var eventElement) &&
            string.Equals(eventElement.GetString(), "READY", StringComparison.Ordinal))
            return;

        var error = root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;

        throw new IOException(
            string.IsNullOrWhiteSpace(error)
                ? "Discord did not complete the Rich Presence handshake."
                : $"Discord IPC handshake failed: {error}");
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        int opcode,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), payload.Length);

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<IpcFrame> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await stream.ReadExactlyAsync(header, cancellationToken);

        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

        if (length < 0 || length > MaximumFrameLength)
            throw new InvalidDataException($"Discord IPC frame length is invalid: {length}.");

        var payload = new byte[length];
        if (length > 0)
            await stream.ReadExactlyAsync(payload, cancellationToken);

        return new IpcFrame(opcode, payload);
    }

    private static void ValidateApplicationId(string applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId) ||
            applicationId.Length is < 16 or > 22 ||
            !ulong.TryParse(applicationId, out _))
        {
            throw new ArgumentException(
                "Enter the numeric Application ID from the Discord Developer Portal.",
                nameof(applicationId));
        }
    }

    private async Task ClosePipeUnsafeAsync()
    {
        if (_pipe is not null)
        {
            try
            {
                await _pipe.DisposeAsync();
            }
            catch (IOException)
            {
                // The remote Discord client may already have closed the pipe.
            }
            finally
            {
                _pipe = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await DisconnectAsync();
        _disposed = true;
        _gate.Dispose();
    }

    private sealed record IpcFrame(int Opcode, byte[] Payload);
}
