using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyLyrics.Application.Outputs;
using SkyLyrics.Application.Settings;
using SkyLyrics.Core.Scene;
using SkyLyrics.Infrastructure.Outputs;

namespace SkyLyrics.Desktop.Outputs;

public sealed class ObsOverlayServerSink : IOutputSink, IHostedService
{
    private const int MaximumHeaderBytes = 8192;
    private static readonly Lazy<string> OverlayHtml = new(LoadOverlayHtml);
    private readonly IAppPreferencesStore _preferences;
    private readonly ILogger<ObsOverlayServerSink> _logger;
    private readonly object _gate = new();
    private SceneState _latest = SceneState.Empty;
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;
    private int _activePort = -1;
    private bool _disposed;

    public ObsOverlayServerSink(
        IAppPreferencesStore preferences,
        ILogger<ObsOverlayServerSink> logger)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _preferences.PreferencesChanged += OnPreferencesChanged;
    }

    private void OnPreferencesChanged(AppPreferences preferences) => EnsureListener();

    public string Name => "OBS Browser Overlay";
    public bool IsEnabled => _preferences.Current.ObsOverlayEnabled;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureListener();
        return Task.CompletedTask;
    }

    public ValueTask PublishAsync(SceneState scene, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _latest, scene ?? throw new ArgumentNullException(nameof(scene)));
        EnsureListener();
        return ValueTask.CompletedTask;
    }

    private void EnsureListener()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var settings = _preferences.Current;
            if (!settings.ObsOverlayEnabled)
            {
                StopListenerLocked();
                return;
            }

            if (_listener is not null && _activePort == settings.ObsOverlayPort)
            {
                return;
            }

            StopListenerLocked();
            var listener = new TcpListener(IPAddress.Loopback, settings.ObsOverlayPort);
            try
            {
                listener.Start(backlog: 8);
                _listener = listener;
                _activePort = settings.ObsOverlayPort;
                _serverCancellation = new CancellationTokenSource();
                var token = _serverCancellation.Token;
                _serverTask = Task.Run(() => AcceptLoopAsync(listener, token), CancellationToken.None);
                _logger.LogInformation(
                    "OBS browser overlay is listening on http://127.0.0.1:{Port}/overlay.",
                    _activePort);
            }
            catch (SocketException exception)
            {
                listener.Stop();
                _listener = null;
                _activePort = -1;
                _serverCancellation?.Dispose();
                _serverCancellation = null;
                _logger.LogWarning(
                    exception,
                    "OBS overlay could not bind to loopback port {Port}.",
                    settings.ObsOverlayPort);
            }
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await HandleClientAsync(client, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    client.Dispose();
                    break;
                }
                catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException)
                {
                    _logger.LogDebug(exception, "OBS overlay client request ended.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var token = timeout.Token;
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine) || requestLine.Length > 512)
            {
                await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "Bad request", token)
                    .ConfigureAwait(false);
                return;
            }

            var consumed = requestLine.Length;
            while (true)
            {
                var header = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (header is null || header.Length == 0)
                {
                    break;
                }

                consumed += header.Length + 2;
                if (consumed > MaximumHeaderBytes)
                {
                    await WriteResponseAsync(stream, 431, "text/plain; charset=utf-8", "Request headers too large", token)
                        .ConfigureAwait(false);
                    return;
                }
            }

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "GET is required", token)
                    .ConfigureAwait(false);
                return;
            }

            var path = parts[1].Split('?', 2)[0];
            switch (path)
            {
                case "/":
                case "/overlay":
                    await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", OverlayHtml.Value, token)
                        .ConfigureAwait(false);
                    break;
                case "/state":
                    var state = ObsOverlayProjection.Map(Volatile.Read(ref _latest));
                    var json = JsonSerializer.Serialize(state);
                    await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", json, token)
                        .ConfigureAwait(false);
                    break;
                case "/health":
                    await WriteResponseAsync(stream, 200, "text/plain; charset=utf-8", "OK", token)
                        .ConfigureAwait(false);
                    break;
                default:
                    await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "Not found", token)
                        .ConfigureAwait(false);
                    break;
            }
        }
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int status,
        string contentType,
        string body,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            431 => "Request Header Fields Too Large",
            _ => "Error"
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string LoadOverlayHtml()
    {
        const string resourceName = "SkyLyrics.Desktop.Assets.obs-overlay.html";
        using var stream = typeof(ObsOverlayServerSink).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded OBS overlay resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private void StopListenerLocked()
    {
        _serverCancellation?.Cancel();
        _listener?.Stop();
        _listener = null;
        _activePort = -1;
        _serverCancellation?.Dispose();
        _serverCancellation = null;
        _serverTask = null;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Task? serverTask;
        lock (_gate)
        {
            serverTask = _serverTask;
            StopListenerLocked();
        }

        return serverTask is null
            ? Task.CompletedTask
            : serverTask.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _preferences.PreferencesChanged -= OnPreferencesChanged;
        Task? serverTask;
        lock (_gate)
        {
            serverTask = _serverTask;
            StopListenerLocked();
        }

        if (serverTask is not null)
        {
            try { await serverTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }
}
