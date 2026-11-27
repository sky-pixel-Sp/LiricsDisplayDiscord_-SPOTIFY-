using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LyricsFileParserTests
{
    private readonly LyricsFileParser _parser = new();

    [Fact]
    public void ParsesOfficialStyleWordSynchronizedDocument()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Still Alive"
              artist: "Example Artist"
              album: "Portal"
              duration_ms: 176457
              language: "en"
            lines:
              - text: "This was a triumph."
                start_ms: 290
                end_ms: 3840
                words:
                  - text: "This "
                    start_ms: 290
                    end_ms: 536
                  - text: "was "
                    start_ms: 536
                    end_ms: 784
                  - text: "a "
                    start_ms: 784
                    end_ms: 923
                  - text: "triumph."
                    start_ms: 923
                    end_ms: 3840
            plain: |
              This was a triumph.
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal("Still Alive", result.Title);
        Assert.Equal("Example Artist", result.Artist);
        Assert.Equal(TimeSpan.FromMilliseconds(176457), result.Duration);
        Assert.Equal(LyricsSyncMode.WordSynced, result.SyncMode);
        Assert.Equal(4, result.Lines[0].Words.Length);
        Assert.Equal("This was a triumph.", string.Concat(result.Lines[0].Words.Select(word => word.Text)));
    }

    [Fact]
    public void ParsesPlainLyricsFileAsUnsynced()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Plain Track"
              artist: "Plain Artist"
            plain: |
              First line
              Second line
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal(LyricsSyncMode.Unsynced, result.SyncMode);
        Assert.Equal(2, result.Lines.Length);
        Assert.All(result.Lines, line => Assert.Null(line.Start));
        Assert.All(result.Lines, line => Assert.Empty(line.Words));
    }

    [Fact]
    public void PreservesOverlappingLines()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Duet"
              artist: "Two Artists"
            lines:
              - text: "Voice one"
                start_ms: 1000
                end_ms: 4000
              - text: "Voice two"
                start_ms: 2000
                end_ms: 3500
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal(2, result.Lines.Length);
        Assert.Equal(TimeSpan.FromSeconds(1), result.Lines[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Lines[1].Start);
        Assert.True(result.Lines[0].End > result.Lines[1].Start);
    }

    [Fact]
    public void SortsUnorderedLinesAndAddsDiagnostic()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Ordered Later"
              artist: "Test"
            lines:
              - text: "Later"
                start_ms: 5000
              - text: "Earlier"
                start_ms: 1000
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal("Earlier", result.Lines[0].Text);
        Assert.Contains(result.Diagnostics, message => message.Contains("sorted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DoesNotApplyUnresolvedOffsetMetadata()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Offset"
              artist: "Test"
              offset_ms: -100
            lines:
              - text: "Line"
                start_ms: 1000
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal(TimeSpan.FromSeconds(1), result.Lines[0].Start);
        Assert.Contains(result.Diagnostics, message => message.Contains("offset_ms", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnsupportedFormatVersion()
    {
        const string yaml = """
            version: "2.0"
            metadata:
              title: "Unknown"
              artist: "Test"
            """;

        Assert.Throws<LyricsParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void RejectsMissingRequiredMetadata()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "No Artist"
            plain: "Text"
            """;

        Assert.Throws<LyricsParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void RejectsNegativeLineTimestamp()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Invalid"
              artist: "Test"
            lines:
              - text: "Line"
                start_ms: -10
            """;

        Assert.Throws<LyricsParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void RejectsEndTimeEarlierThanStartTime()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Invalid"
              artist: "Test"
            lines:
              - text: "Line"
                start_ms: 2000
                end_ms: 1000
            """;

        Assert.Throws<LyricsParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void PreservesWordTextMismatchAndAddsDiagnosticInsteadOfRewritingWords()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Mismatch"
              artist: "Test"
            lines:
              - text: "Original line"
                start_ms: 100
                words:
                  - text: "Different "
                    start_ms: 100
                  - text: "words"
                    start_ms: 300
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal("Original line", result.Lines[0].Text);
        Assert.Equal("Different words", string.Concat(result.Lines[0].Words.Select(word => word.Text)));
        Assert.Contains(result.Diagnostics, message => message.Contains("does not exactly match", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateYamlKeys()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "One"
              title: "Two"
              artist: "Test"
            plain: "Text"
            """;

        Assert.Throws<LyricsParseException>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void ParsesInstrumentalDocumentWithoutCreatingLyrics()
    {
        const string yaml = """
            version: "1.0"
            metadata:
              title: "Instrumental"
              artist: "Composer"
              instrumental: true
            """;

        var result = _parser.Parse(yaml);

        Assert.True(result.Instrumental);
        Assert.Equal(LyricsSyncMode.Instrumental, result.SyncMode);
        Assert.Empty(result.Lines);
    }
}
