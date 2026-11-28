namespace SkyLyrics.Application.Settings;

public enum CinematicQualityLevel
{
    Cinematic = 0,
    Balanced = 1,
    Lite = 2,
    Reduced = 3
}

public sealed class AdaptiveQualityController
{
    private const int MinimumSamplesPerWindow = 10;
    private const int HealthyWindowsToRecover = 3;
    private const double DegradeThresholdMilliseconds = 20d;
    private const double RecoverThresholdMilliseconds = 14d;

    private readonly TimeProvider _timeProvider;
    private readonly CinematicQualityLevel _preferredLevel;
    private DateTimeOffset _windowStartedAt;
    private TimeSpan _totalFrameTime;
    private int _sampleCount;
    private int _healthyWindows;

    public AdaptiveQualityController(
        CinematicQualityLevel initialLevel = CinematicQualityLevel.Balanced,
        TimeProvider? timeProvider = null)
    {
        Level = initialLevel;
        _preferredLevel = initialLevel;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _windowStartedAt = _timeProvider.GetUtcNow();
    }

    public CinematicQualityLevel Level { get; private set; }

    public double LastWindowAverageMilliseconds { get; private set; }

    public bool ObserveFrame(TimeSpan frameTime)
    {
        if (frameTime < TimeSpan.Zero || frameTime > TimeSpan.FromSeconds(1))
        {
            return false;
        }

        _totalFrameTime += frameTime;
        _sampleCount++;

        var now = _timeProvider.GetUtcNow();
        if (now - _windowStartedAt < TimeSpan.FromSeconds(2))
        {
            return false;
        }

        var samplesInWindow = _sampleCount;
        LastWindowAverageMilliseconds = samplesInWindow == 0
            ? 0d
            : _totalFrameTime.TotalMilliseconds / samplesInWindow;

        _windowStartedAt = now;
        _totalFrameTime = TimeSpan.Zero;
        _sampleCount = 0;

        // A sparse window is not reliable enough to trigger either a downgrade or recovery.
        if (samplesInWindow < MinimumSamplesPerWindow)
        {
            _healthyWindows = 0;
            return false;
        }

        if (LastWindowAverageMilliseconds > DegradeThresholdMilliseconds)
        {
            _healthyWindows = 0;
            if (Level == CinematicQualityLevel.Reduced)
            {
                return false;
            }

            Level = (CinematicQualityLevel)Math.Min(
                (int)CinematicQualityLevel.Reduced,
                (int)Level + 1);
            return true;
        }

        // Keep a wide dead band between downgrade and recovery to avoid tier flapping.
        if (LastWindowAverageMilliseconds <= RecoverThresholdMilliseconds &&
            Level > _preferredLevel)
        {
            _healthyWindows++;
            if (_healthyWindows < HealthyWindowsToRecover)
            {
                return false;
            }

            Level = (CinematicQualityLevel)((int)Level - 1);
            _healthyWindows = 0;
            return true;
        }

        _healthyWindows = 0;
        return false;
    }
}
