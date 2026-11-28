namespace SkyLyrics.Application.Startup;

public sealed class SplashDismissalState
{
    private int _isDismissed;

    public bool IsDismissed => Volatile.Read(ref _isDismissed) != 0;

    public bool TryDismiss() => Interlocked.Exchange(ref _isDismissed, 1) == 0;
}
