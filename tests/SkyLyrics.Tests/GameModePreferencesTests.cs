using SkyLyrics.Application.Settings;

namespace SkyLyrics.Tests;

public sealed class GameModePreferencesTests
{
    [Fact]
    public void Normalize_ClampsWindowSizeAndOpacity()
    {
        var result = new GameModePreferences
        {
            Width = 120,
            Height = 900,
            Opacity = 0.1
        }.Normalize();

        Assert.Equal(420d, result.Width);
        Assert.Equal(600d, result.Height);
        Assert.Equal(0.55d, result.Opacity);
    }

    [Fact]
    public void Normalize_RejectsUnknownVisualPresets()
    {
        var result = new GameModePreferences
        {
            BackgroundId = "missing-background",
            TextEffectId = "missing-effect",
            AnimationId = "missing-animation"
        }.Normalize();

        Assert.Equal("aurora", result.BackgroundId);
        Assert.Equal("mint-glow", result.TextEffectId);
        Assert.Equal("cinematic", result.AnimationId);
    }

    [Fact]
    public void Normalize_PreservesValidGeometryAndTopmostPreference()
    {
        var result = new GameModePreferences
        {
            Left = 140,
            Top = 260,
            Width = 840,
            Height = 164,
            Opacity = 0.82,
            AlwaysOnTop = false
        }.Normalize();

        Assert.Equal(140d, result.Left);
        Assert.Equal(260d, result.Top);
        Assert.Equal(840d, result.Width);
        Assert.Equal(164d, result.Height);
        Assert.Equal(0.82d, result.Opacity);
        Assert.False(result.AlwaysOnTop);
    }
}
