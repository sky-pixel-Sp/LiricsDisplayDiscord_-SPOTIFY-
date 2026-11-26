using SkyLyrics.Core.Models;

namespace SkyLyrics.Core.Lyrics;

public static class LyricsSyncEngine
{
    public static int? FindActiveLineIndex(
        LyricsDocument document,
        TimeSpan playbackPosition,
        TimeSpan? userOffset = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.HasLineTimestamps)
            return null;

        var adjustedPosition = playbackPosition + (userOffset ?? TimeSpan.Zero);
        var low = 0;
        var high = document.Lines.Count - 1;
        var activeIndex = -1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var timestamp = document.Lines[mid].StartTime!.Value;

            if (timestamp <= adjustedPosition)
            {
                activeIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return activeIndex >= 0 ? activeIndex : null;
    }
}
