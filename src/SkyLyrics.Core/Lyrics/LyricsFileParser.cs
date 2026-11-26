using SkyLyrics.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkyLyrics.Core.Lyrics;

/// <summary>
/// Parses LRCLIB's Lyricsfile YAML format. Only timestamps explicitly present
/// in the source document are used; missing timestamps are never estimated.
/// </summary>
public static class LyricsFileParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static LyricsDocument Parse(string yaml, string source = "LRCLIB Lyricsfile")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        LyricsFileDto document;
        try
        {
            document = Deserializer.Deserialize<LyricsFileDto>(yaml)
                ?? throw new FormatException("The Lyricsfile document is empty.");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new FormatException("The Lyricsfile document is not valid YAML.", ex);
        }

        if (!string.IsNullOrWhiteSpace(document.Version) &&
            !string.Equals(document.Version.Trim(), "1.0", StringComparison.Ordinal))
        {
            throw new FormatException($"Unsupported Lyricsfile version '{document.Version}'. Expected 1.0.");
        }

        if (document.Lines is null || document.Lines.Count == 0)
            throw new FormatException("The Lyricsfile document contains no lyric lines.");

        var lines = new List<LyricLine>(document.Lines.Count);

        foreach (var sourceLine in document.Lines)
        {
            if (string.IsNullOrEmpty(sourceLine.Text))
                throw new FormatException("A Lyricsfile line is missing its text.");

            IReadOnlyList<LyricWord>? words = null;

            if (sourceLine.Words is { Count: > 0 })
            {
                var parsedWords = new List<LyricWord>(sourceLine.Words.Count);
                long? previousStartMs = null;

                foreach (var sourceWord in sourceLine.Words)
                {
                    if (string.IsNullOrEmpty(sourceWord.Text))
                        throw new FormatException("A word in the Lyricsfile is missing its text.");

                    if (sourceWord.StartMs is not long startMs)
                        throw new FormatException("A word in the Lyricsfile is missing its source start_ms timestamp.");

                    if (startMs < 0)
                        throw new FormatException("Lyricsfile timestamps cannot be negative.");

                    if (previousStartMs is long previous && startMs < previous)
                        throw new FormatException("Word timestamps must be ordered within each lyric line.");

                    var start = TimeSpan.FromMilliseconds(startMs);
                    var end = ReadOptionalTimestamp(sourceWord.EndMs, "word end_ms");

                    if (end is not null && end < start)
                        throw new FormatException("A word end_ms timestamp cannot precede its start_ms timestamp.");

                    parsedWords.Add(new LyricWord(sourceWord.Text, start, end));
                    previousStartMs = startMs;
                }

                words = parsedWords;
            }

            var lineStart = ReadOptionalTimestamp(sourceLine.StartMs, "line start_ms")
                ?? words?.FirstOrDefault()?.StartTime;
            var lineEnd = ReadOptionalTimestamp(sourceLine.EndMs, "line end_ms");

            if (lineStart is not null && lineEnd is not null && lineEnd < lineStart)
                throw new FormatException("A line end_ms timestamp cannot precede its start_ms timestamp.");

            lines.Add(new LyricLine(sourceLine.Text, lineStart, words));
        }

        // Keep timeline order deterministic for the binary-search sync engine.
        // Stable ordering preserves source order for simultaneous/overlapping lines.
        var orderedLines = lines
            .Select((line, index) => (Line: line, Index: index))
            .OrderBy(item => item.Line.StartTime ?? TimeSpan.MaxValue)
            .ThenBy(item => item.Index)
            .Select(item => item.Line)
            .ToArray();

        var hasWordTimestamps = orderedLines.Any(line => line.Words is { Count: > 0 });

        return new LyricsDocument(
            orderedLines,
            document.Metadata?.Title,
            document.Metadata?.Artist,
            hasWordTimestamps,
            source);
    }

    private static TimeSpan? ReadOptionalTimestamp(long? milliseconds, string label)
    {
        if (milliseconds is null)
            return null;

        if (milliseconds < 0)
            throw new FormatException($"Lyricsfile {label} cannot be negative.");

        return TimeSpan.FromMilliseconds(milliseconds.Value);
    }

    private sealed class LyricsFileDto
    {
        public string? Version { get; set; }
        public LyricsFileMetadataDto? Metadata { get; set; }
        public List<LyricsFileLineDto>? Lines { get; set; }
    }

    private sealed class LyricsFileMetadataDto
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
    }

    private sealed class LyricsFileLineDto
    {
        public string? Text { get; set; }
        public long? StartMs { get; set; }
        public long? EndMs { get; set; }
        public List<LyricsFileWordDto>? Words { get; set; }
    }

    private sealed class LyricsFileWordDto
    {
        public string? Text { get; set; }
        public long? StartMs { get; set; }
        public long? EndMs { get; set; }
    }
}
