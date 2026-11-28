namespace SkyLyrics.Application.Playback;

public enum MediaSessionSourceKind
{
    SpotifyDesktop = 0,
    BrowserMediaSession = 1,
    WindowsMedia = 2,
    Other = 3
}

public sealed record MediaSessionSourceIdentity(
    string AppUserModelId,
    MediaSessionSourceKind Kind,
    string DisplayName,
    bool IsSupported);

/// <summary>
/// Classifies exact GSMTC application identities. Browser identities are deliberately
/// labelled as browser media sessions; they do not prove that YouTube Music is the site.
/// </summary>
public static class MediaSessionIdentityMatcher
{
    private static readonly string[] SpotifyIdentities =
    [
        "Spotify.exe",
        "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
        "SpotifyAB.SpotifyMusic_zpdnekdrzrea0"
    ];

    private static readonly (string Identity, string Name)[] BrowserIdentities =
    [
        ("chrome.exe", "Chrome"),
        ("msedge.exe", "Microsoft Edge"),
        ("firefox.exe", "Firefox"),
        ("brave.exe", "Brave"),
        ("opera.exe", "Opera"),
        ("vivaldi.exe", "Vivaldi"),
        ("Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe!App", "Microsoft Edge")
    ];

    private static readonly (string Identity, string Name)[] WindowsMediaIdentities =
    [
        ("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Windows Media"),
        ("Microsoft.ZuneMusic_8wekyb3d8bbwe", "Windows Media"),
        ("Microsoft.Media.Player_8wekyb3d8bbwe!Microsoft.Media.Player", "Windows Media Player"),
        ("Microsoft.Media.Player_8wekyb3d8bbwe", "Windows Media Player"),
        ("wmplayer.exe", "Windows Media Player"),
        ("Microsoft.WindowsMediaPlayer_8wekyb3d8bbwe!WindowsMediaPlayer", "Windows Media Player")
    ];

    public static MediaSessionSourceIdentity Resolve(string? sourceAppUserModelId)
    {
        var identity = sourceAppUserModelId?.Trim() ?? string.Empty;
        if (identity.Length == 0)
        {
            return new MediaSessionSourceIdentity(
                string.Empty,
                MediaSessionSourceKind.Other,
                "Unknown media session",
                false);
        }

        if (Contains(SpotifyIdentities, identity))
        {
            return new MediaSessionSourceIdentity(
                identity,
                MediaSessionSourceKind.SpotifyDesktop,
                "Spotify Desktop",
                true);
        }

        foreach (var (knownIdentity, name) in WindowsMediaIdentities)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(identity, knownIdentity))
            {
                return new MediaSessionSourceIdentity(identity, MediaSessionSourceKind.WindowsMedia, name, true);
            }
        }

        foreach (var (knownIdentity, name) in BrowserIdentities)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(identity, knownIdentity))
            {
                return new MediaSessionSourceIdentity(
                    identity,
                    MediaSessionSourceKind.BrowserMediaSession,
                    $"Browser Media Session · {name}",
                    true);
            }
        }

        return new MediaSessionSourceIdentity(
            identity,
            MediaSessionSourceKind.Other,
            identity,
            false);
    }

    private static bool Contains(IEnumerable<string> identities, string candidate) =>
        identities.Contains(candidate, StringComparer.OrdinalIgnoreCase);
}
