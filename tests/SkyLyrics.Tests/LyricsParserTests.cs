using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Core.Models;

namespace SkyLyrics.Tests;

[TestClass]
public sealed class LyricsParserTests
{
    [TestMethod]
    public void ParsesLineTimestampsAndMetadata()
    {
        const string lrc = "[ti:Sample]\n[ar:Artist]\n[00:01.25]First line\n[00:03.500]Second line";
        var document = LrcParser.Parse(lrc);

        Assert.AreEqual("Sample", document.Title);
        Assert.AreEqual("Artist", document.Artist);
        Assert.AreEqual(2, document.Lines.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1250), document.Lines[0].StartTime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3500), document.Lines[1].StartTime);
        Assert.IsTrue(document.HasLineTimestamps);
        Assert.IsFalse(document.HasWordTimestamps);
    }

    [TestMethod]
    public void SupportsMultipleTimestampsOnOneLineAndOffset()
    {
        const string lrc = "[offset:500]\n[00:01.00][00:02.00]Repeated";
        var document = LrcParser.Parse(lrc);

        Assert.AreEqual(2, document.Lines.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), document.Lines[0].StartTime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), document.Lines[1].StartTime);
    }

    [TestMethod]
    public void PlainLyricsRemainUnsynced()
    {
        var document = LyricsDocument.FromPlainText("One line\nSecond line");

        Assert.IsTrue(document.IsUnsynced);
        Assert.IsFalse(document.HasLineTimestamps);
        Assert.IsNull(LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void FindsLatestLineAtOrBeforePlaybackPosition()
    {
        var document = LrcParser.Parse("[00:01.00]One\n[00:03.00]Two\n[00:05.00]Three");

        Assert.IsNull(LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromMilliseconds(999)));
        Assert.AreEqual(0, LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromSeconds(1)));
        Assert.AreEqual(1, LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromSeconds(4)));
        Assert.AreEqual(2, LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void ParsesLyricsFileWordTimestampsAndFindsActiveWord()
    {
        const string yaml = """
            version: '1.0'
            metadata:
              title: 'Sample'
              artist: 'Artist'
              duration_ms: 5000
            lines:
              - text: 'Hello there'
                start_ms: 1000
                end_ms: 1800
                words:
                  - text: 'Hello '
                    start_ms: 1000
                    end_ms: 1400
                  - text: 'there'
                    start_ms: 1450
                    end_ms: 1800
            plain: |
              Hello there
            """;

        var document = LyricsFileParser.Parse(yaml);
        var line = document.Lines[0];

        Assert.AreEqual("Sample", document.Title);
        Assert.AreEqual("Artist", document.Artist);
        Assert.IsTrue(document.HasLineTimestamps);
        Assert.IsTrue(document.HasWordTimestamps);
        Assert.AreEqual(2, line.Words!.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000), line.Words[0].StartTime);
        Assert.AreEqual(0, LyricsSyncEngine.FindActiveWordIndex(line, TimeSpan.FromMilliseconds(1200)));
        Assert.AreEqual(1, LyricsSyncEngine.FindActiveWordIndex(line, TimeSpan.FromMilliseconds(1500)));
        Assert.IsNull(LyricsSyncEngine.FindActiveWordIndex(line, TimeSpan.FromMilliseconds(900)));
    }

    [TestMethod]
    public void LyricsFileWithoutSourceTimestampsRemainsUnsynced()
    {
        const string yaml = """
            version: '1.0'
            lines:
              - text: 'First line'
              - text: 'Second line'
            plain: |
              First line
              Second line
            """;

        var document = LyricsFileParser.Parse(yaml);

        Assert.IsTrue(document.IsUnsynced);
        Assert.IsFalse(document.HasLineTimestamps);
        Assert.IsFalse(document.HasWordTimestamps);
        Assert.IsNull(LyricsSyncEngine.FindActiveLineIndex(document, TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public void RejectsWordSyncWhenWordTimestampIsMissing()
    {
        const string yaml = """
            version: '1.0'
            lines:
              - text: 'Missing timing'
                start_ms: 1000
                words:
                  - text: 'Missing '
                    start_ms: 1000
                  - text: 'timing'
            """;

        var threwExpectedException = false;
        try
        {
            LyricsFileParser.Parse(yaml);
        }
        catch (FormatException)
        {
            threwExpectedException = true;
        }

        Assert.IsTrue(threwExpectedException, "Invalid word timing must be rejected instead of guessed.");
    }
}
