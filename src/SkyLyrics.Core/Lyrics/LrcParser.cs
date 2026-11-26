using System.Globalization;
using System.Text.RegularExpressions;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Core.Lyrics;

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"^\[(?<key>[a-zA-Z]+):(?<value>[^\]]*)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex MetadataRegex();

    public static LyricsDocument Parse(string content, string source = "local .lrc")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string? title = null;
        string? artist = null;
        var offsetMs = 0;
        var parsed = new List<(TimeSpan Timestamp, string Text, int Order)>();
        var unsynced = new List<LyricLine>();
        var order = 0;

        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var metadata = MetadataRegex().Match(line);
            if (metadata.Success)
            {
                var key = metadata.Groups["key"].Value.ToLowerInvariant();
                var value = metadata.Groups["value"].Value.Trim();

                switch (key)
                {
                    case "ti": title = value; break;
                    case "ar": artist = value; break;
                    case "offset":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOffset))
                            offsetMs = parsedOffset;
                        break;
                }
                continue;
            }

            var matches = TimestampRegex().Matches(line);
            if (matches.Count == 0)
            {
                // Untimed text remains visible but is never treated as synchronized.
                if (!line.StartsWith('['))
                    unsynced.Add(new LyricLine(line, null));
                continue;
            }

            var text = TimestampRegex().Replace(line, string.Empty).Trim();
            if (text.Length == 0)
                continue;

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
                var fraction = match.Groups["fraction"].Value;
                var milliseconds = fraction.Length switch
                {
                    1 => int.Parse(fraction, CultureInfo.InvariantCulture) * 100,
                    2 => int.Parse(fraction, CultureInfo.InvariantCulture) * 10,
                    3 => int.Parse(fraction, CultureInfo.InvariantCulture),
                    _ => 0
                };

                var totalMs = Math.Max(0, (minutes * 60_000L) + (seconds * 1_000L) + milliseconds + offsetMs);
                parsed.Add((TimeSpan.FromMilliseconds(totalMs), text, order++));
            }
        }

        var timedLines = parsed
            .OrderBy(item => item.Timestamp)
            .ThenBy(item => item.Order)
            .Select(item => new LyricLine(item.Text, item.Timestamp));

        var lines = parsed.Count > 0 ? timedLines.ToArray() : unsynced.ToArray();
        if (lines.Length == 0)
            throw new FormatException("The selected LRC file contains no lyric lines.");

        return new LyricsDocument(lines, title, artist, false, source);
    }
}
