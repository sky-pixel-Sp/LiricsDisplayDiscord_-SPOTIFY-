using SkyLyrics.Application.Startup;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class SplashDismissalStateTests
{
    [Fact]
    public void SplashCanBeDismissedOnlyOnce()
    {
        var state = new SplashDismissalState();

        Assert.False(state.IsDismissed);
        Assert.True(state.TryDismiss());
        Assert.True(state.IsDismissed);
        Assert.False(state.TryDismiss());
    }

    [Fact]
    public async Task ConcurrentSkipAndFinishCannotReopenTheSplash()
    {
        var state = new SplashDismissalState();
        var outcomes = await Task.WhenAll(
            Task.Run(state.TryDismiss),
            Task.Run(state.TryDismiss),
            Task.Run(state.TryDismiss));

        Assert.Single(outcomes, static result => result);
        Assert.True(state.IsDismissed);
        Assert.False(state.TryDismiss());
    }
}
