using System.Collections.Immutable;

namespace SkyLyrics.Core.Lyrics;

public enum LyricsSyncMode
{
    Unsynced = 0,
    LineSynced = 1,
    WordSynced = 2,
    Instrumental = 3
}

public sealed record LyricWord
{
    public LyricWord(TimeSpan start, TimeSpan? end, string text)
    {
        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end is not null && end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        ArgumentNullException.ThrowIfNull(text);

        Start = start;
        End = end;
        Text = text;
    }

    public TimeSpan Start { get; }
    public TimeSpan? End { get; }
    public string Text { get; }
}

public sealed record LyricLine
{
    public LyricLine(
        string text,
        TimeSpan? start,
        TimeSpan? end = null,
        IEnumerable<LyricWord>? words = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (start is not null && start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end is not null && (start is null || end < start))
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        Words = words?.ToImmutableArray() ?? ImmutableArray<LyricWord>.Empty;
        if (Words.Any(static word => word is null))
        {
            throw new ArgumentException("A lyric line cannot contain a null word.", nameof(words));
        }

        if (!Words.IsEmpty && start is null)
        {
            throw new ArgumentException("Word timing requires a timestamped lyric line.", nameof(words));
        }

        Text = text;
        Start = start;
        End = end;
    }

    public string Text { get; }
    public TimeSpan? Start { get; }
    public TimeSpan? End { get; }
    public ImmutableArray<LyricWord> Words { get; }
}

public sealed record LyricsDocument
{
    public LyricsDocument(
        string? title,
        string? artist,
        string? album,
        TimeSpan? duration,
        bool instrumental,
        IEnumerable<LyricLine>? lines,
        string? plainText = null,
        IEnumerable<string>? diagnostics = null,
        string? language = null)
    {
        if (duration is not null && duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Lines = lines?.ToImmutableArray() ?? ImmutableArray<LyricLine>.Empty;
        if (Lines.Any(static line => line is null))
        {
            throw new ArgumentException("A lyrics document cannot contain a null line.", nameof(lines));
        }

        Title = NormalizeOptional(title);
        Artist = NormalizeOptional(artist);
        Album = NormalizeOptional(album);
        Duration = duration;
        Instrumental = instrumental;
        PlainText = plainText;
        Language = NormalizeOptional(language);
        Diagnostics = diagnostics?.ToImmutableArray() ?? ImmutableArray<string>.Empty;

        if (Instrumental && (Lines.Any(static line => !string.IsNullOrWhiteSpace(line.Text)) ||
                            !string.IsNullOrWhiteSpace(PlainText)))
        {
            throw new ArgumentException("Instrumental documents cannot contain vocal lyrics.");
        }
    }

    public string? Title { get; }
    public string? Artist { get; }
    public string? Album { get; }
    public TimeSpan? Duration { get; }
    public bool Instrumental { get; }
    public ImmutableArray<LyricLine> Lines { get; }
    public string? PlainText { get; }
    public ImmutableArray<string> Diagnostics { get; }
    public string? Language { get; }

    public LyricsSyncMode SyncMode
    {
        get
        {
            if (Instrumental)
            {
                return LyricsSyncMode.Instrumental;
            }

            if (Lines.Any(static line => !line.Words.IsEmpty))
            {
                return LyricsSyncMode.WordSynced;
            }

            if (Lines.Any(static line => line.Start is not null))
            {
                return LyricsSyncMode.LineSynced;
            }

            return LyricsSyncMode.Unsynced;
        }
    }

    public static LyricsDocument FromPlainText(
        string? title,
        string? artist,
        string? album,
        TimeSpan? duration,
        string text,
        IEnumerable<string>? diagnostics = null,
        string? language = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(static value => new LyricLine(value, null));

        return new LyricsDocument(
            title,
            artist,
            album,
            duration,
            instrumental: false,
            lines,
            text,
            diagnostics,
            language);
    }

    public static LyricsDocument CreateInstrumental(
        string? title,
        string? artist,
        string? album,
        TimeSpan? duration,
        IEnumerable<string>? diagnostics = null) =>
        new(title, artist, album, duration, instrumental: true, null, diagnostics: diagnostics);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
