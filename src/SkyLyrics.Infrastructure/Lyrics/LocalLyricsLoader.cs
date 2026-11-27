using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LocalLyricsLoader
{
    private const long MaximumFileBytes = 2 * 1024 * 1024;

    private readonly LrcParser _lrcParser;
    private readonly LyricsFileParser _lyricsFileParser;

    public LocalLyricsLoader(LrcParser lrcParser, LyricsFileParser lyricsFileParser)
    {
        _lrcParser = lrcParser;
        _lyricsFileParser = lyricsFileParser;
    }

    public async Task<LyricsDocument> LoadAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var extension = Path.GetExtension(fullPath);
        var isLyricsFile = fullPath.EndsWith(".lyricsfile.yaml", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase);
        var isLrc = string.Equals(extension, ".lrc", StringComparison.OrdinalIgnoreCase);

        if (!isLyricsFile && !isLrc)
        {
            throw new LyricsParseException("Unsupported file type. Select an .lrc, .yaml, .yml or .lyricsfile.yaml file.");
        }

        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Selected lyrics file was not found.", fullPath);
        }

        if (info.Length > MaximumFileBytes)
        {
            throw new LyricsParseException("Lyrics file exceeds the 2 MiB import limit.");
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return isLyricsFile
            ? _lyricsFileParser.Parse(content)
            : _lrcParser.Parse(content);
    }
}
