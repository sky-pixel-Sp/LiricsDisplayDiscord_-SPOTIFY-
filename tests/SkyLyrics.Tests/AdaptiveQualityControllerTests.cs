using SkyLyrics.Application.Settings;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class AdaptiveQualityControllerTests
{
    [Fact]
    public void DowngradesOnlyAfterTwoSecondAverageExceedsTwentyMilliseconds()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Cinematic, time);

        for (var index = 0; index < 20; index++)
        {
            Assert.False(monitor.ObserveFrame(TimeSpan.FromMilliseconds(25)));
            time.Advance(TimeSpan.FromMilliseconds(90));
        }

        Assert.Equal(CinematicQualityLevel.Cinematic, monitor.Level);
        Assert.True(monitor.ObserveFrame(TimeSpan.FromMilliseconds(25)));
        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);
        Assert.True(monitor.LastWindowAverageMilliseconds > 20d);
    }

    [Fact]
    public void KeepsSelectedTierWhenAverageFrameTimeIsWithinBudget()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Balanced, time);

        for (var index = 0; index < 22; index++)
        {
            monitor.ObserveFrame(TimeSpan.FromMilliseconds(16));
            time.Advance(TimeSpan.FromMilliseconds(95));
        }

        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);
        Assert.InRange(monitor.LastWindowAverageMilliseconds, 15d, 17d);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
