using Microsoft.Extensions.Logging;
using SkyLyrics.Core.Scene;

namespace SkyLyrics.Application.Outputs;

public interface IOutputSink : IAsyncDisposable
{
    string Name { get; }
    bool IsEnabled { get; }
    ValueTask PublishAsync(SceneState scene, CancellationToken cancellationToken);
}

public sealed class SceneStateRouter
{
    private readonly IOutputSink[] _sinks;
    private readonly ILogger<SceneStateRouter> _logger;

    public SceneStateRouter(IEnumerable<IOutputSink> sinks, ILogger<SceneStateRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentNullException.ThrowIfNull(logger);

        _sinks = sinks.ToArray();
        _logger = logger;

        var duplicateNames = _sinks
            .GroupBy(static sink => sink.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicateNames.Length > 0)
        {
            throw new ArgumentException(
                $"Output sink names must be unique. Duplicates: {string.Join(", ", duplicateNames)}",
                nameof(sinks));
        }
    }

    public async ValueTask PublishAsync(SceneState scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        cancellationToken.ThrowIfCancellationRequested();

        var deliveries = _sinks
            .Where(static sink => sink.IsEnabled)
            .Select(sink => PublishSafelyAsync(sink, scene, cancellationToken));

        await Task.WhenAll(deliveries).ConfigureAwait(false);
    }

    private async Task PublishSafelyAsync(
        IOutputSink sink,
        SceneState scene,
        CancellationToken cancellationToken)
    {
        try
        {
            await sink.PublishAsync(scene, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Output sink {SinkName} failed while publishing scene revision {SceneRevision}.",
                sink.Name,
                scene.Revision);
        }
    }
}
