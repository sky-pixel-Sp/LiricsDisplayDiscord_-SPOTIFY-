using System.Globalization;
using System.Text.RegularExpressions;
using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LrcParser
{
    private static readonly Regex MetadataTagRegex = new(
        @"^\[(?<tag>[a-zA-Z]+):(?<value>[^\]]*)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public LyricsDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var diagnostics = new List<string>();
        var timedLines = new List<(TimeSpan Start, string Text, int Order)>();
        var untimedLines = new List<string>();
        string? title = null;
        string? artist = null;
        string? album = null;
        TimeSpan? duration = null;
        long offsetMilliseconds = 0;
        var order = 0;

        var sourceLines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var rawLine in sourceLines)
        {
            var remaining = rawLine.TrimStart();
            if (remaining.Length == 0)
            {
                continue;
            }

            var timestamps = new List<TimeSpan>();
            var cursor = 0;

            while (cursor < remaining.Length && remaining[cursor] == '[')
            {
                var closing = remaining.IndexOf(']', cursor + 1);
                if (closing < 0)
                {
                    diagnostics.Add($"Ignored malformed tag on source line {order + 1}.");
                    break;
                }

                var token = remaining[(cursor + 1)..closing];
                if (TryParseTimestamp(token, out var timestamp))
                {
                    timestamps.Add(timestamp);
                    cursor = closing + 1;
                    continue;
                }

                var metadataMatch = MetadataTagRegex.Match(remaining[cursor..]);
                if (metadataMatch.Success && metadataMatch.Length == closing - cursor + 1)
                {
                    ApplyMetadata(
                        metadataMatch.Groups["tag"].Value,
                        metadataMatch.Groups["value"].Value,
                        ref title,
                        ref artist,
                        ref album,
                        ref duration,
                        ref offsetMilliseconds,
                        diagnostics);
                    cursor += metadataMatch.Length;
                    continue;
                }

                break;
            }

            var lyricText = remaining[cursor..].TrimStart();
            if (timestamps.Count > 0)
            {
                foreach (var timestamp in timestamps)
                {
                    var adjusted = AddOffset(timestamp, offsetMilliseconds, diagnostics);
                    timedLines.Add((adjusted, lyricText, order++));
                }
            }
            else if (!string.IsNullOrWhiteSpace(lyricText))
            {
                untimedLines.Add(lyricText);
                order++;
            }
        }

        if (timedLines.Count > 0)
        {
            if (untimedLines.Count > 0)
            {
                diagnostics.Add(
                    $"Ignored {untimedLines.Count} untimestamped lyric line(s) because timestamped lines were present.");
            }

            var lines = timedLines
                .OrderBy(static item => item.Start)
                .ThenBy(static item => item.Order)
                .Select(static item => new LyricLine(item.Text, item.Start));

            return new LyricsDocument(
                title,
                artist,
                album,
                duration,
                instrumental: false,
                lines,
                diagnostics: diagnostics);
        }

        var plain = string.Join(Environment.NewLine, untimedLines);
        return LyricsDocument.FromPlainText(
            title,
            artist,
            album,
            duration,
            plain,
            diagnostics);
    }

    private static void ApplyMetadata(
        string tag,
        string value,
        ref string? title,
        ref string? artist,
        ref string? album,
        ref TimeSpan? duration,
        ref long offsetMilliseconds,
        ICollection<string> diagnostics)
    {
        switch (tag.ToLowerInvariant())
        {
            case "ti":
                title = value.Trim();
                break;
            case "ar":
                artist = value.Trim();
                break;
            case "al":
                album = value.Trim();
                break;
            case "length":
                if (TryParseTimestamp(value, out var parsedDuration))
                {
                    duration = parsedDuration;
                }
                else
                {
                    diagnostics.Add("Ignored invalid LRC length metadata.");
                }
                break;
            case "offset":
                if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOffset))
                {
                    offsetMilliseconds = parsedOffset;
                }
                else
                {
                    diagnostics.Add("Ignored invalid LRC offset metadata.");
                }
                break;
        }
    }

    private static TimeSpan AddOffset(
        TimeSpan timestamp,
        long offsetMilliseconds,
        ICollection<string> diagnostics)
    {
        try
        {
            var adjustedTicks = checked(timestamp.Ticks + checked(offsetMilliseconds * TimeSpan.TicksPerMillisecond));
            if (adjustedTicks < 0)
            {
                diagnostics.Add("A negative timestamp after LRC offset was clamped to zero.");
                return TimeSpan.Zero;
            }

            return TimeSpan.FromTicks(adjustedTicks);
        }
        catch (OverflowException)
        {
            diagnostics.Add("A timestamp overflowed after LRC offset and was clamped.");
            return offsetMilliseconds < 0 ? TimeSpan.Zero : TimeSpan.MaxValue;
        }
    }

    private static bool TryParseTimestamp(string value, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        var parts = value.Split(':');
        if (parts.Length is not (2 or 3))
        {
            return false;
        }

        var secondsText = parts[^1];
        var secondsParts = secondsText.Split('.', ',');
        if (secondsParts.Length > 2 ||
            !int.TryParse(secondsParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds is < 0 or >= 60)
        {
            return false;
        }

        var milliseconds = 0;
        if (secondsParts.Length == 2)
        {
            var fraction = secondsParts[1];
            if (fraction.Length is < 1 or > 3 ||
                !int.TryParse(fraction.PadRight(3, '0'), NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }
        }

        if (!int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 0)
        {
            return false;
        }

        long totalMilliseconds;
        try
        {
            if (parts.Length == 2)
            {
                totalMilliseconds = checked(((long)minutes * 60 + seconds) * 1000 + milliseconds);
            }
            else
            {
                if (minutes >= 60 ||
                    !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
                    hours < 0)
                {
                    return false;
                }

                totalMilliseconds = checked((((hours * 60) + minutes) * 60 + seconds) * 1000 + milliseconds);
            }

            timestamp = TimeSpan.FromMilliseconds(totalMilliseconds);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
