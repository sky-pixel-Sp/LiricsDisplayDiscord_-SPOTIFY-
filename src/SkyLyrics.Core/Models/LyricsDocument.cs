namespace SkyLyrics.Core.Models;

public sealed record LyricWord(string Text, TimeSpan StartTime, TimeSpan? EndTime = null);
public sealed record LyricLine(string Text, TimeSpan? StartTime, IReadOnlyList<LyricWord>? Words = null);

public sealed record LyricsDocument(
    IReadOnlyList<LyricLine> Lines,
    string? Title = null,
    string? Artist = null,
    bool HasWordTimestamps = false,
    string Source = "unknown")
{
    public bool HasLineTimestamps => Lines.Count > 0 && Lines.All(line => line.StartTime.HasValue);
    public bool IsUnsynced => Lines.Count > 0 && !HasLineTimestamps;

    public static LyricsDocument FromPlainText(string text, string? title = null, string? artist = null, string source = "LRCLIB")
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None)
            .Select(line => new LyricLine(line.TrimEnd(), null))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .ToArray();
        return new LyricsDocument(lines, title, artist, false, source);
    }
}
