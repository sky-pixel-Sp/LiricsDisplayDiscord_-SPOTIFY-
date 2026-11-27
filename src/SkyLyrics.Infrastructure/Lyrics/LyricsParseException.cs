namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LyricsParseException : FormatException
{
    public LyricsParseException(string message)
        : base(message)
    {
    }

    public LyricsParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
