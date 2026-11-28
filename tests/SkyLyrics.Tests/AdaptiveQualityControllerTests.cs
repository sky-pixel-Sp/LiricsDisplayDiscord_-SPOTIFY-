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
            time.Advance(TimeSpan.FromMilliseconds(100));
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

        for (var index = 0; index < 20; index++)
        {
            monitor.ObserveFrame(TimeSpan.FromMilliseconds(16));
            time.Advance(TimeSpan.FromMilliseconds(100));
        }

        monitor.ObserveFrame(TimeSpan.FromMilliseconds(16));

        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);
        Assert.InRange(monitor.LastWindowAverageMilliseconds, 15d, 17d);
    }

    [Fact]
    public void DowngradesFromBalancedToLiteAndReducedOnSustainedSlowFrames()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Balanced, time);

        Assert.True(ObserveWindow(monitor, time, 30d));
        Assert.Equal(CinematicQualityLevel.Lite, monitor.Level);

        Assert.True(ObserveWindow(monitor, time, 30d));
        Assert.Equal(CinematicQualityLevel.Reduced, monitor.Level);

        Assert.False(ObserveWindow(monitor, time, 30d));
        Assert.Equal(CinematicQualityLevel.Reduced, monitor.Level);
    }

    [Fact]
    public void GraduallyRecoversAfterThreeHealthyWindowsWithoutExceedingPreferredTier()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Balanced, time);

        Assert.True(ObserveWindow(monitor, time, 30d));
        Assert.True(ObserveWindow(monitor, time, 30d));
        Assert.Equal(CinematicQualityLevel.Reduced, monitor.Level);

        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.True(ObserveWindow(monitor, time, 14d));
        Assert.Equal(CinematicQualityLevel.Lite, monitor.Level);

        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.True(ObserveWindow(monitor, time, 14d));
        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);

        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);
    }

    [Fact]
    public void NeverRecoversAboveAnExplicitlySelectedLiteTier()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Lite, time);

        Assert.True(ObserveWindow(monitor, time, 25d));
        Assert.Equal(CinematicQualityLevel.Reduced, monitor.Level);

        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.True(ObserveWindow(monitor, time, 14d));
        Assert.Equal(CinematicQualityLevel.Lite, monitor.Level);

        for (var index = 0; index < 4; index++)
        {
            Assert.False(ObserveWindow(monitor, time, 14d));
        }

        Assert.Equal(CinematicQualityLevel.Lite, monitor.Level);
    }

    [Fact]
    public void ExplicitReducedTierStaysLocked()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Reduced, time);

        for (var index = 0; index < 6; index++)
        {
            Assert.False(ObserveWindow(monitor, time, 10d));
        }

        Assert.Equal(CinematicQualityLevel.Reduced, monitor.Level);
    }

    [Fact]
    public void AModerateWindowResetsRecoveryHysteresis()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Balanced, time);

        Assert.True(ObserveWindow(monitor, time, 25d));
        Assert.True(ObserveWindow(monitor, time, 25d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.False(ObserveWindow(monitor, time, 18d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.False(ObserveWindow(monitor, time, 14d));
        Assert.True(ObserveWindow(monitor, time, 14d));
        Assert.Equal(CinematicQualityLevel.Lite, monitor.Level);
    }

    [Fact]
    public void SparseWindowDoesNotChangeQuality()
    {
        var time = new ManualTimeProvider();
        var monitor = new AdaptiveQualityController(CinematicQualityLevel.Balanced, time);

        Assert.False(monitor.ObserveFrame(TimeSpan.FromMilliseconds(28)));
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(monitor.ObserveFrame(TimeSpan.FromMilliseconds(12)));

        Assert.Equal(CinematicQualityLevel.Balanced, monitor.Level);
    }

    private static bool ObserveWindow(
        AdaptiveQualityController monitor,
        ManualTimeProvider time,
        double frameTimeMilliseconds)
    {
        var changed = false;
        for (var index = 0; index < 20; index++)
        {
            changed |= monitor.ObserveFrame(TimeSpan.FromMilliseconds(frameTimeMilliseconds));
            time.Advance(TimeSpan.FromMilliseconds(100));
        }

        return monitor.ObserveFrame(TimeSpan.FromMilliseconds(frameTimeMilliseconds)) || changed;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
