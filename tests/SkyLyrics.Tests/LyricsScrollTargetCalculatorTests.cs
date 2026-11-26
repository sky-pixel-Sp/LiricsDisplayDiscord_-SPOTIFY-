using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class LyricsScrollTargetCalculatorTests
{
    [TestMethod]
    public void CalculatesFromAbsoluteContentCoordinateWithoutAddingCurrentOffset()
    {
        var target = LyricsScrollTargetCalculator.Calculate(
            lineCenterContentY: 1_200,
            viewportHeight: 600,
            scrollableHeight: 2_400);

        Assert.AreEqual(912, target, 0.001);
    }

    [TestMethod]
    public void ClampsBeforeFirstLineToTop()
    {
        var target = LyricsScrollTargetCalculator.Calculate(
            lineCenterContentY: 100,
            viewportHeight: 600,
            scrollableHeight: 2_400);

        Assert.AreEqual(0, target, 0.001);
    }

    [TestMethod]
    public void ClampsAfterLastLineToBottom()
    {
        var target = LyricsScrollTargetCalculator.Calculate(
            lineCenterContentY: 4_000,
            viewportHeight: 600,
            scrollableHeight: 2_400);

        Assert.AreEqual(2_400, target, 0.001);
    }

    [TestMethod]
    public void RejectsInvalidCoordinates()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LyricsScrollTargetCalculator.Calculate(double.NaN, 600, 2_400));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LyricsScrollTargetCalculator.Calculate(1_200, -1, 2_400));
    }
}
