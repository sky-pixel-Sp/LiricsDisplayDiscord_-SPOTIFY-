using SkyLyrics.Core.Lyrics;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LyricsFileParser
{
    private const int MaximumDocumentCharacters = 2 * 1024 * 1024;
    private const long MaximumTimestampMilliseconds = TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond;

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .WithMaximumRecursion(32)
        .Build();

    public LyricsDocument Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        if (yaml.Length > MaximumDocumentCharacters)
        {
            throw new LyricsParseException("Lyricsfile exceeds the 2 MiB document limit.");
        }

        LyricsFileYamlDto? parsed;
        try
        {
            parsed = _deserializer.Deserialize<LyricsFileYamlDto>(yaml);
        }
        catch (YamlException exception)
        {
            throw new LyricsParseException("Lyricsfile YAML is invalid or contains duplicate keys.", exception);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new LyricsParseException("Lyricsfile could not be converted to the supported version 1.0 structure.", exception);
        }

        if (parsed is null)
        {
            throw new LyricsParseException("Lyricsfile is empty.");
        }

        if (!string.Equals(parsed.Version, "1.0", StringComparison.Ordinal))
        {
            throw new LyricsParseException(
                $"Unsupported Lyricsfile version '{parsed.Version ?? "(missing)"}'. Only version 1.0 is supported.");
        }

        if (parsed.Metadata is null ||
            string.IsNullOrWhiteSpace(parsed.Metadata.Title) ||
            string.IsNullOrWhiteSpace(parsed.Metadata.Artist))
        {
            throw new LyricsParseException("Lyricsfile metadata must contain both title and artist.");
        }

        if (parsed.Metadata.DurationMs is < 0)
        {
            throw new LyricsParseException("Lyricsfile duration_ms cannot be negative.");
        }

        if (parsed.Metadata.DurationMs is { } durationValue && durationValue > MaximumTimestampMilliseconds)
        {
            throw new LyricsParseException("Lyricsfile duration_ms exceeds the supported TimeSpan range.");
        }

        TimeSpan? duration = parsed.Metadata.DurationMs is { } durationMs
            ? TimeSpan.FromMilliseconds(durationMs)
            : null;

        var diagnostics = new List<string>();
        if (parsed.Metadata.OffsetMs is not null)
        {
            diagnostics.Add("metadata.offset_ms was preserved as unsupported metadata and was not applied; its timing semantics are unresolved in Lyricsfile 1.0.");
        }

        var yamlLines = parsed.Lines ?? [];
        if (parsed.Metadata.Instrumental == true)
        {
            if (yamlLines.Count > 0 || !string.IsNullOrWhiteSpace(parsed.Plain))
            {
                throw new LyricsParseException("An instrumental Lyricsfile must not contain lines or plain lyrics.");
            }

            return LyricsDocument.CreateInstrumental(
                parsed.Metadata.Title,
                parsed.Metadata.Artist,
                parsed.Metadata.Album,
                duration,
                diagnostics);
        }

        var lines = new List<LyricLine>(yamlLines.Count);
        var previousLineStart = -1L;

        foreach (var yamlLine in yamlLines)
        {
            if (yamlLine is null || yamlLine.Text is null || yamlLine.StartMs is null)
            {
                throw new LyricsParseException("Each synchronized line requires text and start_ms.");
            }

            var startMs = yamlLine.StartMs.Value;
            if (startMs > MaximumTimestampMilliseconds)
            {
                throw new LyricsParseException("Line start_ms exceeds the supported TimeSpan range.");
            }

            if (startMs < 0)
            {
                throw new LyricsParseException("Line start_ms cannot be negative.");
            }

            if (yamlLine.EndMs is { } endMs && endMs > MaximumTimestampMilliseconds)
            {
                throw new LyricsParseException("Line end_ms exceeds the supported TimeSpan range.");
            }

            if (yamlLine.EndMs is { } invalidEnd && invalidEnd < startMs)
            {
                throw new LyricsParseException("Line end_ms cannot be earlier than line start_ms.");
            }

            if (startMs < previousLineStart)
            {
                diagnostics.Add("Lyricsfile lines were not ordered by start_ms; lines were sorted for playback.");
            }

            previousLineStart = startMs;

            var words = new List<LyricWord>();
            var previousWordStart = -1L;
            foreach (var yamlWord in yamlLine.Words ?? [])
            {
                if (yamlWord is null || yamlWord.Text is null || yamlWord.StartMs is null)
                {
                    throw new LyricsParseException("Each synchronized word requires text and start_ms.");
                }

                var wordStart = yamlWord.StartMs.Value;
                if (wordStart > MaximumTimestampMilliseconds)
                {
                    throw new LyricsParseException("Word start_ms exceeds the supported TimeSpan range.");
                }

                if (wordStart < 0)
                {
                    throw new LyricsParseException("Word start_ms cannot be negative.");
                }

                if (yamlWord.EndMs is { } wordEnd && wordEnd > MaximumTimestampMilliseconds)
                {
                    throw new LyricsParseException("Word end_ms exceeds the supported TimeSpan range.");
                }

                if (yamlWord.EndMs is { } invalidWordEnd && invalidWordEnd < wordStart)
                {
                    throw new LyricsParseException("Word end_ms cannot be earlier than word start_ms.");
                }

                if (wordStart < previousWordStart)
                {
                    diagnostics.Add("A word sequence was not ordered by start_ms; its original text order was preserved.");
                }

                previousWordStart = wordStart;
                words.Add(new LyricWord(
                    TimeSpan.FromMilliseconds(wordStart),
                    yamlWord.EndMs is { } end ? TimeSpan.FromMilliseconds(end) : null,
                    yamlWord.Text));
            }

            if (words.Count > 0 &&
                !string.Equals(string.Concat(words.Select(static word => word.Text)), yamlLine.Text, StringComparison.Ordinal))
            {
                diagnostics.Add("A line's concatenated word text does not exactly match line.text; both source values were preserved.");
            }

            lines.Add(new LyricLine(
                yamlLine.Text,
                TimeSpan.FromMilliseconds(startMs),
                yamlLine.EndMs is { } lineEnd ? TimeSpan.FromMilliseconds(lineEnd) : null,
                words));
        }

        lines = lines
            .Select((line, index) => (line, index))
            .OrderBy(static item => item.line.Start)
            .ThenBy(static item => item.index)
            .Select(static item => item.line)
            .ToList();

        var plain = parsed.Plain;
        if (lines.Count == 0 && plain is not null)
        {
            return LyricsDocument.FromPlainText(
                parsed.Metadata.Title,
                parsed.Metadata.Artist,
                parsed.Metadata.Album,
                duration,
                plain,
                diagnostics,
                parsed.Metadata.Language);
        }

        if (lines.Count == 0 && string.IsNullOrWhiteSpace(plain))
        {
            throw new LyricsParseException("Lyricsfile contains neither synchronized lines nor plain lyrics.");
        }

        return new LyricsDocument(
            parsed.Metadata.Title,
            parsed.Metadata.Artist,
            parsed.Metadata.Album,
            duration,
            instrumental: false,
            lines,
            plain,
            diagnostics,
            parsed.Metadata.Language);
    }
}

public sealed class LyricsFileYamlDto
{
    public string? Version { get; set; }
    public LyricsFileMetadataYamlDto? Metadata { get; set; }
    public List<LyricsFileLineYamlDto>? Lines { get; set; }
    public string? Plain { get; set; }
}

public sealed class LyricsFileMetadataYamlDto
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public long? DurationMs { get; set; }
    public long? OffsetMs { get; set; }
    public string? Language { get; set; }
    public bool? Instrumental { get; set; }
}

public sealed class LyricsFileLineYamlDto
{
    public string? Text { get; set; }
    public long? StartMs { get; set; }
    public long? EndMs { get; set; }
    public List<LyricsFileWordYamlDto>? Words { get; set; }
}

public sealed class LyricsFileWordYamlDto
{
    public string? Text { get; set; }
    public long? StartMs { get; set; }
    public long? EndMs { get; set; }
}
