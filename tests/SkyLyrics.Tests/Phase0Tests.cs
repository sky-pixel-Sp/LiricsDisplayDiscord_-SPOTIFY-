using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Outputs;
using SkyLyrics.Core.Scene;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class Phase0Tests
{
    [Fact]
    public void EmptySceneHasNoTrackOrLyrics()
    {
        var scene = SceneState.Empty;

        Assert.Null(scene.Track);
        Assert.Null(scene.ActiveLine);
        Assert.Null(scene.NextLine);
        Assert.Null(scene.WordFill);
        Assert.Equal(TimeSpan.Zero, scene.Position);
        Assert.Equal(PlaybackStatus.Unknown, scene.PlaybackStatus);
    }

    [Fact]
    public void SceneRejectsNegativePlaybackPosition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneState(
            1, null, null, null, null, TimeSpan.FromMilliseconds(-1),
            PlaybackStatus.Playing, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void WordFillRequiresAnActiveLine()
    {
        var word = new LyricWordScene(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "word");
        var fill = new WordFillState(word, 0.5);

        Assert.Throws<ArgumentException>(() => new SceneState(
            1, null, null, null, fill, TimeSpan.FromSeconds(1.5),
            PlaybackStatus.Playing, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void LyricLineCopiesWordTimingsIntoAnImmutableArray()
    {
        var sourceWords = new List<LyricWordScene>
        {
            new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.4), "first")
        };

        var line = new LyricLineScene(TimeSpan.FromSeconds(1), "first second", sourceWords);
        sourceWords.Clear();

        Assert.Single(line.Words);
        Assert.Equal("first", line.Words[0].Text);
    }

    [Fact]
    public void WordFillRejectsProgressOutsideZeroToOne()
    {
        var word = new LyricWordScene(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "word");

        Assert.Throws<ArgumentOutOfRangeException>(() => new WordFillState(word, 1.01));
    }

    [Fact]
    public async Task RouterPublishesTheSameSceneToEnabledSinks()
    {
        var scene = CreateScene();
        var wpf = new RecordingSink("WPF");
        var obs = new RecordingSink("OBS");
        var disabled = new RecordingSink("Discord", isEnabled: false);
        var router = CreateRouter(wpf, obs, disabled);

        await router.PublishAsync(scene);

        Assert.Same(scene, Assert.Single(wpf.Received));
        Assert.Same(scene, Assert.Single(obs.Received));
        Assert.Empty(disabled.Received);
    }

    [Fact]
    public async Task SinkFailureDoesNotBlockOtherOutputs()
    {
        var scene = CreateScene();
        var failing = new RecordingSink("BrokenOverlay")
        {
            Failure = new InvalidOperationException("Expected test failure.")
        };
        var healthy = new RecordingSink("WPF");
        var router = CreateRouter(failing, healthy);

        var exception = await Record.ExceptionAsync(() => router.PublishAsync(scene).AsTask());

        Assert.Null(exception);
        Assert.Same(scene, Assert.Single(healthy.Received));
    }

    [Fact]
    public async Task RouterHonorsCancellationBeforePublishing()
    {
        var sink = new RecordingSink("WPF");
        var router = CreateRouter(sink);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.PublishAsync(CreateScene(), cancellation.Token).AsTask());

        Assert.Empty(sink.Received);
    }

    private static SceneStateRouter CreateRouter(params IOutputSink[] sinks) =>
        new(sinks, NullLogger<SceneStateRouter>.Instance);

    private static SceneState CreateScene() =>
        new(
            7,
            new TrackScene("Track", "Artist", "Album", null),
            null,
            null,
            null,
            TimeSpan.FromSeconds(12),
            PlaybackStatus.Playing,
            DateTimeOffset.UtcNow);

    private sealed class RecordingSink(string name, bool isEnabled = true) : IOutputSink
    {
        public string Name { get; } = name;
        public bool IsEnabled { get; } = isEnabled;
        public List<SceneState> Received { get; } = [];
        public Exception? Failure { get; init; }

        public ValueTask PublishAsync(SceneState scene, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Failure is not null)
            {
                return ValueTask.FromException(Failure);
            }

            Received.Add(scene);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
