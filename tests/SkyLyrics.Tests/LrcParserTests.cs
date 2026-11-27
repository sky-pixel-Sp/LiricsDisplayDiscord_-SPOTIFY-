using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LrcParserTests
{
    private readonly LrcParser _parser = new();

    [Fact]
    public void ParsesMetadataAndTimestampedLines()
    {
        const string source = """
            [ti:Midnight]
            [ar:Example Artist]
            [al:First Album]
            [length:03:20.00]
            [00:01.25]First line
            [00:05.50]Second line
            """;

        var result = _parser.Parse(source);

        Assert.Equal("Midnight", result.Title);
        Assert.Equal("Example Artist", result.Artist);
        Assert.Equal("First Album", result.Album);
        Assert.Equal(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(20), result.Duration);
        Assert.Equal(LyricsSyncMode.LineSynced, result.SyncMode);
        Assert.Equal(2, result.Lines.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), result.Lines[0].Start);
        Assert.Equal("First line", result.Lines[0].Text);
    }

    [Fact]
    public void ExpandsMultipleTimestampsOnOneSourceLine()
    {
        var result = _parser.Parse("[00:01.20][00:02.50]Repeated phrase");

        Assert.Equal(2, result.Lines.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), result.Lines[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), result.Lines[1].Start);
        Assert.All(result.Lines, line => Assert.Equal("Repeated phrase", line.Text));
    }

    [Fact]
    public void ParsesHourMinuteSecondTimestamp()
    {
        var result = _parser.Parse("[01:02:03.456]Long track");

        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) +
                     TimeSpan.FromSeconds(3) + TimeSpan.FromMilliseconds(456),
            result.Lines[0].Start);
    }

    [Fact]
    public void ParsesOneAndTwoDigitFractionalSecondsAsTenthsAndHundredths()
    {
        var result = _parser.Parse("""
            [00:01.2]Tenths
            [00:02.34]Hundredths
            """);

        Assert.Equal(TimeSpan.FromMilliseconds(1200), result.Lines[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2340), result.Lines[1].Start);
    }

    [Fact]
    public void AppliesLrcOffsetWithoutInventingWordTimings()
    {
        var result = _parser.Parse("""
            [offset:250]
            [00:01.00]Offset line
            """);

        Assert.Equal(TimeSpan.FromMilliseconds(1250), result.Lines[0].Start);
        Assert.Empty(result.Lines[0].Words);
    }

    [Fact]
    public void ClampsNegativeOffsetResultsToZeroAndReportsDiagnostic()
    {
        var result = _parser.Parse("""
            [offset:-200]
            [00:00.10]Clamped line
            """);

        Assert.Equal(TimeSpan.Zero, result.Lines[0].Start);
        Assert.Contains(result.Diagnostics, message => message.Contains("clamped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KeepsPlainLyricsUnsyncedWhenNoTimestampTagsExist()
    {
        var result = _parser.Parse("""
            First line
            Second line
            """);

        Assert.Equal(LyricsSyncMode.Unsynced, result.SyncMode);
        Assert.All(result.Lines, line => Assert.Null(line.Start));
        Assert.All(result.Lines, line => Assert.Empty(line.Words));
        Assert.Equal(2, result.Lines.Length);
    }

    [Fact]
    public void SortsTimestampedLinesByTimeAndPreservesEqualTimeOrder()
    {
        var result = _parser.Parse("""
            [00:05.00]Later
            [00:01.00]Earlier
            [00:01.00]Same time second
            """);

        Assert.Equal("Earlier", result.Lines[0].Text);
        Assert.Equal("Same time second", result.Lines[1].Text);
        Assert.Equal("Later", result.Lines[2].Text);
    }

    [Fact]
    public void DoesNotGenerateAnyWordLevelTiming()
    {
        var result = _parser.Parse("[00:00.00]Never split this into fake word timings");

        Assert.Empty(result.Lines[0].Words);
        Assert.Equal(LyricsSyncMode.LineSynced, result.SyncMode);
    }

    [Fact]
    public void IgnoresUntimestampedLinesWhenTimestampedLyricsArePresentAndReportsIt()
    {
        var result = _parser.Parse("""
            [00:01.00]Timestamped
            stray untagged text
            """);

        Assert.Single(result.Lines);
        Assert.Contains(result.Diagnostics, message => message.Contains("untimestamped", StringComparison.OrdinalIgnoreCase));
    }
}
