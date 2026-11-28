namespace SkyLyrics.Core.Models;

public sealed record SpotifyTrackSnapshot(
    string Title,
    string Artist,
    string? Album,
    TimeSpan Position,
    TimeSpan? Duration,
    bool IsPlaying,
    string SourceAppUserModelId)
{
    public string MatchKey => $"{Artist.Trim()}\u001f{Title.Trim()}\u001f{Album?.Trim()}";
}
