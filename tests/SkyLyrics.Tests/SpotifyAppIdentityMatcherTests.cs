using SkyLyrics.Application.Playback;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class SpotifyAppIdentityMatcherTests
{
    [Theory]
    [InlineData("Spotify.exe")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0")]
    public void MatchesKnownSpotifyDesktopIdentities(string identity)
    {
        var matcher = new SpotifyAppIdentityMatcher();

        Assert.True(matcher.IsMatch(identity));
    }

    [Fact]
    public void IdentityMatchingIsCaseInsensitive()
    {
        var matcher = new SpotifyAppIdentityMatcher();

        Assert.True(matcher.IsMatch("spotify.exe"));
    }

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("msedge.exe")]
    [InlineData("Firefox")]
    [InlineData("Spotify Web Player")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotMatchOtherMediaApplications(string? identity)
    {
        var matcher = new SpotifyAppIdentityMatcher();

        Assert.False(matcher.IsMatch(identity));
    }

    [Fact]
    public void SupportsAnExplicitAdditionalIdentityWithoutFuzzyMatching()
    {
        var matcher = new SpotifyAppIdentityMatcher(["Contoso.SpotifyDesktop!App"]);

        Assert.True(matcher.IsMatch("Contoso.SpotifyDesktop!App"));
        Assert.False(matcher.IsMatch("Contoso.SpotifyDesktop!App.Browser"));
    }
}
