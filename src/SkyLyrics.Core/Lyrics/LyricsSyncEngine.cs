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

    public static int? FindActiveWordIndex(
        LyricLine line,
        TimeSpan playbackPosition,
        TimeSpan? userOffset = null)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Words is not { Count: > 0 })
            return null;

        var adjustedPosition = playbackPosition + (userOffset ?? TimeSpan.Zero);
        var activeIndex = -1;

        // Word order is validated by LyricsFileParser. A sequential scan also
        // handles overlapping word durations without inventing word boundaries.
        for (var index = 0; index < line.Words.Count; index++)
        {
            if (line.Words[index].StartTime > adjustedPosition)
                break;

            activeIndex = index;
        }

        return activeIndex >= 0 ? activeIndex : null;
    }
}
