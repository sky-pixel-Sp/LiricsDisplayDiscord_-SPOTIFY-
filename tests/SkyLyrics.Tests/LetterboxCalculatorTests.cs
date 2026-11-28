using SkyLyrics.Application.Settings;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LetterboxCalculatorTests
{
    [Fact]
    public void DisabledLetterboxDoesNotConsumeStageHeight()
    {
        var result = LetterboxCalculator.Calculate(1280d, 720d, enabled: false);

        Assert.Equal(0d, result.Height);
    }

    [Fact]
    public void CalculatesCentered239To1LetterboxForSixteenByNineStage()
    {
        var result = LetterboxCalculator.Calculate(1920d, 1080d, enabled: true);

        Assert.InRange(result.Height, 138.1d, 138.4d);
        var contentHeight = 1080d - (result.Height * 2d);
        Assert.Equal(2.39d, 1920d / contentHeight, precision: 2);
    }

    [Fact]
    public void AddsBarsWhenTheStageIsNarrowerThanTheCinematicContentRatio()
    {
        var result = LetterboxCalculator.Calculate(900d, 500d, enabled: true);

        Assert.InRange(result.Height, 61.5d, 61.8d);
    }

    [Fact]
    public void DoesNotAddBarsToAnAlreadyUltraWideStage()
    {
        var result = LetterboxCalculator.Calculate(1600d, 500d, enabled: true);

        Assert.Equal(0d, result.Height);
    }

    [Theory]
    [InlineData(double.NaN, 600d)]
    [InlineData(1280d, double.PositiveInfinity)]
    [InlineData(0d, 600d)]
    public void InvalidStageDimensionsReturnNoBars(double width, double height)
    {
        var result = LetterboxCalculator.Calculate(width, height, enabled: true);

        Assert.Equal(0d, result.Height);
    }
}
