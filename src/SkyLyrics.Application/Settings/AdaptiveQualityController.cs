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
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _windowStartedAt;
    private TimeSpan _totalFrameTime;
    private int _sampleCount;

    public AdaptiveQualityController(
        CinematicQualityLevel initialLevel = CinematicQualityLevel.Balanced,
        TimeProvider? timeProvider = null)
    {
        Level = initialLevel;
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

        var elapsed = _timeProvider.GetUtcNow() - _windowStartedAt;
        if (elapsed < TimeSpan.FromSeconds(2))
        {
            return false;
        }

        LastWindowAverageMilliseconds = _sampleCount == 0
            ? 0d
            : _totalFrameTime.TotalMilliseconds / _sampleCount;

        _windowStartedAt = _timeProvider.GetUtcNow();
        _totalFrameTime = TimeSpan.Zero;
        _sampleCount = 0;

        if (LastWindowAverageMilliseconds <= 20d || Level == CinematicQualityLevel.Reduced)
        {
            return false;
        }

        Level = Level switch
        {
            CinematicQualityLevel.Cinematic => CinematicQualityLevel.Balanced,
            CinematicQualityLevel.Balanced => CinematicQualityLevel.Lite,
            _ => CinematicQualityLevel.Reduced
        };

        return true;
    }
}
