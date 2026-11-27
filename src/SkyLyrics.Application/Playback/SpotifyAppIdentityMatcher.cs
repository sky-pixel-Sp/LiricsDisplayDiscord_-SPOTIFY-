using System.Collections.Immutable;

namespace SkyLyrics.Application.Playback;

public sealed class SpotifyAppIdentityMatcher
{
    private static readonly ImmutableHashSet<string> DefaultIdentities =
        ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "Spotify.exe",
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0");

    private readonly ImmutableHashSet<string> _identities;

    public SpotifyAppIdentityMatcher(IEnumerable<string>? additionalIdentities = null)
    {
        var builder = DefaultIdentities.ToBuilder();

        if (additionalIdentities is not null)
        {
            foreach (var identity in additionalIdentities)
            {
                if (!string.IsNullOrWhiteSpace(identity))
                {
                    builder.Add(identity.Trim());
                }
            }
        }

        _identities = builder.ToImmutable();
    }

    public bool IsMatch(string? sourceAppUserModelId) =>
        !string.IsNullOrWhiteSpace(sourceAppUserModelId) &&
        _identities.Contains(sourceAppUserModelId.Trim());

    public IReadOnlyCollection<string> KnownIdentities => _identities;
}
