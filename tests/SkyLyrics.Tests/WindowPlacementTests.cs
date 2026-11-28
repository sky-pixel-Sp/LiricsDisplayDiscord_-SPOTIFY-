using SkyLyrics.Application.Settings;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void CenterAndClamp_ShrinksToSmallWorkAreaWhenMinimumDoesNotFit()
    {
        var result = WindowPlacementCalculator.CenterAndClamp(
            new WindowBounds(0d, 0d, 1280d, 800d),
            new WindowBounds(0d, 0d, 900d, 520d),
            minimumWidth: 980d,
            minimumHeight: 610d);

        Assert.Equal(new WindowBounds(0d, 0d, 900d, 520d), result);
        Assert.True(WindowPlacementCalculator.IsFullyVisibleOnAnyMonitor(
            result,
            [new WindowBounds(0d, 0d, 900d, 520d)]));
    }

    [Fact]
    public void CenterAndClamp_UsesTheOriginOfASecondaryMonitor()
    {
        var result = WindowPlacementCalculator.CenterAndClamp(
            new WindowBounds(0d, 0d, 1280d, 800d),
            new WindowBounds(-1920d, 0d, 1920d, 1080d),
            minimumWidth: 980d,
            minimumHeight: 610d);

        Assert.Equal(new WindowBounds(-1600d, 140d, 1280d, 800d), result);
    }

    [Fact]
    public void ClampToWorkArea_RepairsNegativePositionWithoutChangingFittingSize()
    {
        var result = WindowPlacementCalculator.ClampToWorkArea(
            new WindowBounds(-900d, -300d, 1000d, 700d),
            new WindowBounds(0d, 0d, 1440d, 900d),
            minimumWidth: 980d,
            minimumHeight: 610d);

        Assert.Equal(new WindowBounds(0d, 0d, 1000d, 700d), result);
    }

    [Fact]
    public void SecondaryMonitorWorkAreaIsConvertedUsingItsOwnScaleFactor()
    {
        var logical = WindowPlacementCalculator.ConvertPhysicalWorkAreaToLogical(
            new WindowBounds(-2880d, 0d, 2880d, 1620d),
            scaleFactor: 1.5d);

        Assert.Equal(new WindowBounds(-1920d, 0d, 1920d, 1080d), logical);
    }

    [Fact]
    public void ClampToWorkArea_ClampsOversizedBoundsToASecondaryMonitor()
    {
        var result = WindowPlacementCalculator.ClampToWorkArea(
            new WindowBounds(-2200d, -200d, 1920d, 1080d),
            new WindowBounds(-1920d, -120d, 1600d, 900d),
            minimumWidth: 980d,
            minimumHeight: 610d);

        Assert.Equal(new WindowBounds(-1920d, -120d, 1600d, 900d), result);
    }

    [Fact]
    public void SavedGeometryIsAcceptedWhenFullyVisibleOnASecondaryMonitor()
    {
        var saved = new WindowBounds(-1840d, 120d, 900d, 600d);
        WindowBounds[] connectedWorkAreas =
        [
            new(-1920d, 0d, 1920d, 1040d),
            new(0d, 0d, 1920d, 1040d)
        ];

        Assert.True(WindowPlacementCalculator.IsFullyVisibleOnAnyMonitor(saved, connectedWorkAreas));
    }

    [Fact]
    public void SavedGeometryIsRejectedWhenItsMonitorHasBeenDisconnected()
    {
        var saved = new WindowBounds(-1840d, 120d, 900d, 600d);
        WindowBounds[] remainingWorkAreas =
        [
            new(0d, 0d, 1920d, 1040d)
        ];

        Assert.False(WindowPlacementCalculator.IsFullyVisibleOnAnyMonitor(saved, remainingWorkAreas));
    }

    [Fact]
    public void SavedGeometryMustBeFullyInsideOneMonitorWorkArea()
    {
        var saved = new WindowBounds(-80d, 40d, 900d, 600d);
        WindowBounds[] workAreas =
        [
            new(0d, 0d, 1920d, 1040d),
            new(1920d, 0d, 1920d, 1040d)
        ];

        Assert.False(WindowPlacementCalculator.IsFullyVisibleOnAnyMonitor(saved, workAreas));
    }

    [Fact]
    public void AppPreferencesNormalizationDropsInvalidMainWindowGeometry()
    {
        var normalized = new AppPreferences
        {
            MainWindowGeometry = new WindowGeometryPreference(0d, 0d, double.PositiveInfinity, 800d)
        }.Normalize();

        Assert.Null(normalized.MainWindowGeometry);
    }

    [Fact]
    public void AppPreferencesNormalizationPreservesValidMainWindowGeometry()
    {
        var geometry = new WindowGeometryPreference(-1840d, 120d, 900d, 600d);
        var normalized = new AppPreferences { MainWindowGeometry = geometry }.Normalize();

        Assert.Equal(geometry, normalized.MainWindowGeometry);
    }

    [Fact]
    public void MainWindowGeometryNormalizationRejectsInvalidAndNonFiniteValues()
    {
        Assert.Null(new WindowGeometryPreference(double.NaN, 0d, 1280d, 800d).Normalize());
        Assert.Null(new WindowGeometryPreference(0d, 0d, -1d, 800d).Normalize());
        Assert.Equal(
            new WindowGeometryPreference(-1840d, 120d, 900d, 600d),
            new WindowGeometryPreference(-1840d, 120d, 900d, 600d).Normalize());
    }
}
