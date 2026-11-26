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
}
