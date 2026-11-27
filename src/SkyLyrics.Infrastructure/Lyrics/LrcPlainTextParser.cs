using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LrcPlainTextParser
{
    public LyricsDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return LyricsDocument.FromPlainText(null, null, null, null, text);
    }
}
