using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkyLyrics.Core.Lyrics;

public sealed class LrclibClient
{
    private const string ApiBase = "https://lrclib.net/api/get";
    private readonly HttpClient _httpClient;

    public LrclibClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkyLyricsDesktop", "0.1.0"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("(https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-)"));
        }
    }

    public async Task<LrclibLyrics?> GetExactAsync(
        string trackName,
        string artistName,
        string? albumName,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);
        ArgumentException.ThrowIfNullOrWhiteSpace(artistName);

        var query = new List<string>
        {
            $"track_name={Uri.EscapeDataString(trackName)}",
            $"artist_name={Uri.EscapeDataString(artistName)}"
        };

        if (!string.IsNullOrWhiteSpace(albumName))
            query.Add($"album_name={Uri.EscapeDataString(albumName)}");

        if (duration is { } value && value > TimeSpan.Zero)
        {
            var seconds = Math.Round(value.TotalSeconds, MidpointRounding.AwayFromZero)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            query.Add($"duration={seconds}");
        }

        var uri = $"{ApiBase}?{string.Join("&", query)}";
        using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        // 429 and 503 are surfaced to the caller; retry logic must honor Retry-After.
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LrclibLyrics>(
            JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed record LrclibLyrics(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("trackName")] string TrackName,
    [property: JsonPropertyName("artistName")] string ArtistName,
    [property: JsonPropertyName("albumName")] string? AlbumName,
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("instrumental")] bool Instrumental,
    [property: JsonPropertyName("plainLyrics")] string? PlainLyrics,
    [property: JsonPropertyName("syncedLyrics")] string? SyncedLyrics,
    [property: JsonPropertyName("hasWordSync")] bool HasWordSync,
    [property: JsonPropertyName("lyricsfile")] string? LyricsFile);
