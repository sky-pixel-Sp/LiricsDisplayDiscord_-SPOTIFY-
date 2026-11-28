using System.Collections.Immutable;

namespace SkyLyrics.Core.Scene;

public enum PlaybackStatus
{
    Unknown = 0,
    Playing = 1,
    Paused = 2,
    Stopped = 3
}

public sealed record TrackScene(
    string? Title,
    string? Artist,
    string? Album,
    Uri? ArtworkUri,
    ImmutableArray<byte> ArtworkBytes = default)
{
    public bool HasArtwork => !ArtworkBytes.IsDefaultOrEmpty;
}

public sealed record LyricWordScene
{
    public LyricWordScene(TimeSpan start, TimeSpan? end, string text)
    {
        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Word start cannot be negative.");
        }

        if (end is not null && end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Word end must be later than word start.");
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

public sealed record LyricLineScene
{
    public LyricLineScene(
        TimeSpan start,
        string text,
        IEnumerable<LyricWordScene>? words = null,
        int lineIndex = -1)
    {
        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Line start cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(text);
        if (lineIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        Start = start;
        Text = text;
        LineIndex = lineIndex;
        Words = words?.ToImmutableArray() ?? ImmutableArray<LyricWordScene>.Empty;

        if (Words.Any(static word => word is null))
        {
            throw new ArgumentException("A lyric line cannot contain a null word.", nameof(words));
        }
    }

    public TimeSpan Start { get; }
    public string Text { get; }
    public int LineIndex { get; }
    public ImmutableArray<LyricWordScene> Words { get; }
}

public sealed record WordFillState
{
    public WordFillState(LyricWordScene word, double progress)
    {
        ArgumentNullException.ThrowIfNull(word);

        if (!double.IsFinite(progress) || progress is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Word progress must be finite and between 0 and 1.");
        }

        Word = word;
        Progress = progress;
    }

    public LyricWordScene Word { get; }
    public double Progress { get; }
}

public sealed record SceneState
{
    public SceneState(
        long revision,
        TrackScene? track,
        LyricLineScene? activeLine,
        LyricLineScene? nextLine,
        WordFillState? wordFill,
        TimeSpan position,
        PlaybackStatus playbackStatus,
        DateTimeOffset updatedAt,
        LyricLineScene? previousLine = null)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Scene revision cannot be negative.");
        }

        if (position < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Playback position cannot be negative.");
        }

        if (wordFill is not null && activeLine is null)
        {
            throw new ArgumentException("Word fill requires an active lyric line.", nameof(wordFill));
        }

        Revision = revision;
        Track = track;
        PreviousLine = previousLine;
        ActiveLine = activeLine;
        NextLine = nextLine;
        WordFill = wordFill;
        Position = position;
        PlaybackStatus = playbackStatus;
        UpdatedAt = updatedAt;
    }

    public static SceneState Empty { get; } = new(
        0, null, null, null, null, TimeSpan.Zero,
        PlaybackStatus.Unknown, DateTimeOffset.UnixEpoch);

    public long Revision { get; }
    public TrackScene? Track { get; }
    public LyricLineScene? PreviousLine { get; }
    public LyricLineScene? ActiveLine { get; }
    public int ActiveLineIndex => ActiveLine?.LineIndex ?? -1;
    public LyricLineScene? NextLine { get; }
    public WordFillState? WordFill { get; }
    public TimeSpan Position { get; }
    public PlaybackStatus PlaybackStatus { get; }
    public DateTimeOffset UpdatedAt { get; }
}
