using SkyLyrics.Application.Playback;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class MediaSessionIdentityMatcherTests
{
    [Theory]
    [InlineData("Spotify.exe")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    [InlineData("spotifyab.spotifymusic_zpdnekdrzrea0")]
    public void IdentifiesSpotifyDesktop(string identity)
    {
        var result = MediaSessionIdentityMatcher.Resolve(identity);

        Assert.True(result.IsSupported);
        Assert.Equal(MediaSessionSourceKind.SpotifyDesktop, result.Kind);
        Assert.Equal("Spotify Desktop", result.DisplayName);
    }

    [Theory]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("msedge.exe", "Microsoft Edge")]
    [InlineData("firefox.exe", "Firefox")]
    public void IdentifiesBrowserSessionsWithoutClaimingWebsite(string identity, string browser)
    {
        var result = MediaSessionIdentityMatcher.Resolve(identity);

        Assert.True(result.IsSupported);
        Assert.Equal(MediaSessionSourceKind.BrowserMediaSession, result.Kind);
        Assert.Equal($"Browser Media Session · {browser}", result.DisplayName);
        Assert.False(result.DisplayName.Contains("YouTube", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("wmplayer.exe", "Windows Media Player")]
    [InlineData("Microsoft.Media.Player_8wekyb3d8bbwe", "Windows Media Player")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe", "Windows Media")]
    public void IdentifiesWindowsMediaPlayers(string identity, string expectedName)
    {
        var result = MediaSessionIdentityMatcher.Resolve(identity);

        Assert.True(result.IsSupported);
        Assert.Equal(MediaSessionSourceKind.WindowsMedia, result.Kind);
        Assert.Equal(expectedName, result.DisplayName);
    }

    [Fact]
    public void TrimsIdentityBeforeMatching()
    {
        var result = MediaSessionIdentityMatcher.Resolve("  Spotify.exe  ");

        Assert.Equal(MediaSessionSourceKind.SpotifyDesktop, result.Kind);
    }

    [Theory]
    [InlineData("teams.exe")]
    [InlineData("some-random-app")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotGuessUnknownApplicationIdentity(string? identity)
    {
        var result = MediaSessionIdentityMatcher.Resolve(identity);

        Assert.False(result.IsSupported);
        Assert.Equal(MediaSessionSourceKind.Other, result.Kind);
    }
}
