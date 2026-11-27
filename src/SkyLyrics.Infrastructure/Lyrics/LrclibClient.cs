using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;

namespace SkyLyrics.Infrastructure.Lyrics;

public sealed class LrclibClient : ILyricsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(300);
    private const int MaximumRateLimitRetries = 2;

    private readonly HttpClient _httpClient;
    private readonly LrcParser _lrcParser;
    private readonly LyricsFileParser _lyricsFileParser;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly Func<double> _jitterSource;
    private long _lastRequestTimestamp;

    public LrclibClient(
        HttpClient httpClient,
        LrcParser lrcParser,
        LyricsFileParser lyricsFileParser,
        TimeProvider timeProvider,
        Func<double>? jitterSource = null)
    {
        _httpClient = httpClient;
        _lrcParser = lrcParser;
        _lyricsFileParser = lyricsFileParser;
        _timeProvider = timeProvider;
        _jitterSource = jitterSource ?? (() => Random.Shared.NextDouble());
    }

    public string Name => "LRCLIB";

    public async Task<LyricsProviderResult?> GetExactAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken)
    {
        ValidateSignature(signature);

        if (!string.IsNullOrWhiteSpace(signature.Album) &&
            signature.Duration is { } cachedDuration &&
            cachedDuration.TotalSeconds is >= 1 and <= 3600)
        {
            var cachedUri = BuildSignatureUri("api/get-cached", signature, requireAlbumAndDuration: true);
            var cachedRecord = await GetRecordAsync(cachedUri, cancellationToken).ConfigureAwait(false);
            if (cachedRecord is not null)
            {
                return ConvertRecord(cachedRecord);
            }
        }

        var exactUri = BuildSignatureUri("api/get", signature, requireAlbumAndDuration: false);
        var record = await GetRecordAsync(exactUri, cancellationToken).ConfigureAwait(false);
        return record is null ? null : ConvertRecord(record);
    }

    public async Task<IReadOnlyList<LyricsProviderResult>> SearchAsync(
        LyricsTrackSignature signature,
        CancellationToken cancellationToken)
    {
        ValidateSignature(signature);

        var query = new List<string>
        {
            $"track_name={Uri.EscapeDataString(signature.Title.Trim())}",
            $"artist_name={Uri.EscapeDataString(signature.Artist.Trim())}"
        };

        if (!string.IsNullOrWhiteSpace(signature.Album))
        {
            query.Add($"album_name={Uri.EscapeDataString(signature.Album.Trim())}");
        }

        var relativeUri = $"api/search?{string.Join("&", query)}";
        using var response = await SendSequentiallyAsync(
            () => new HttpRequestMessage(HttpMethod.Get, relativeUri),
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var records = JsonSerializer.Deserialize<List<LrclibApiRecord>>(body, JsonOptions) ?? [];
        var results = new List<LyricsProviderResult>(records.Count);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var converted = ConvertRecord(record);
            if (converted is not null)
            {
                results.Add(converted);
            }
        }

        return results;
    }

    public async Task<LyricsProviderResult?> GetByIdAsync(
        int providerId,
        CancellationToken cancellationToken)
    {
        if (providerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(providerId));
        }

        var record = await GetRecordAsync($"api/get/{providerId.ToString(CultureInfo.InvariantCulture)}", cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ConvertRecord(record);
    }

    private async Task<LrclibApiRecord?> GetRecordAsync(
        string relativeUri,
        CancellationToken cancellationToken)
    {
        using var response = await SendSequentiallyAsync(
            () => new HttpRequestMessage(HttpMethod.Get, relativeUri),
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<LrclibApiRecord>(body, JsonOptions)
            ?? throw new InvalidDataException("LRCLIB returned an empty or invalid lyrics record.");
    }

    private async Task<HttpResponseMessage> SendSequentiallyAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                await EnforceMinimumRequestIntervalAsync(cancellationToken).ConfigureAwait(false);
                _lastRequestTimestamp = _timeProvider.GetTimestamp();

                using var request = requestFactory();
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    throw;
                }

                if (response.StatusCode != HttpStatusCode.TooManyRequests ||
                    attempt >= MaximumRateLimitRetries)
                {
                    return response;
                }

                var retryDelay = GetRetryDelay(response);
                response.Dispose();

                var jitterFactor = Math.Clamp(_jitterSource(), 0d, 1d) * 0.3d;
                var delay = TimeSpan.FromMilliseconds(
                    retryDelay.TotalMilliseconds * (1d + jitterFactor));

                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task EnforceMinimumRequestIntervalAsync(CancellationToken cancellationToken)
    {
        if (_lastRequestTimestamp == 0)
        {
            return;
        }

        var elapsed = _timeProvider.GetElapsedTime(
            _lastRequestTimestamp,
            _timeProvider.GetTimestamp());

        var remaining = MinimumRequestInterval - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - _timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return TimeSpan.FromSeconds(5);
    }

    private LyricsProviderResult? ConvertRecord(LrclibApiRecord record)
    {
        TimeSpan? duration = record.Duration is { } seconds && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

        LyricsDocument? document = null;
        var diagnostics = new List<string>();

        if (record.Instrumental == true)
        {
            document = LyricsDocument.CreateInstrumental(
                record.TrackName ?? record.Name,
                record.ArtistName,
                record.AlbumName,
                duration);
        }
        else if (!string.IsNullOrWhiteSpace(record.Lyricsfile))
        {
            try
            {
                document = _lyricsFileParser.Parse(record.Lyricsfile);
            }
            catch (LyricsParseException exception)
            {
                diagnostics.Add($"LRCLIB Lyricsfile could not be parsed; a supported fallback field was used: {exception.Message}");
            }
        }

        if (document is null && !string.IsNullOrWhiteSpace(record.SyncedLyrics))
        {
            document = _lrcParser.Parse(record.SyncedLyrics);
        }

        if (document is null && !string.IsNullOrWhiteSpace(record.PlainLyrics))
        {
            document = LyricsDocument.FromPlainText(
                record.TrackName ?? record.Name,
                record.ArtistName,
                record.AlbumName,
                duration,
                record.PlainLyrics);
        }

        if (document is null)
        {
            return null;
        }

        if (diagnostics.Count > 0)
        {
            diagnostics.AddRange(document.Diagnostics);
        }
        else
        {
            diagnostics.AddRange(document.Diagnostics);
        }

        var normalized = new LyricsDocument(
            record.TrackName ?? document.Title,
            record.ArtistName ?? document.Artist,
            record.AlbumName ?? document.Album,
            duration ?? document.Duration,
            document.Instrumental,
            document.Lines,
            document.PlainText,
            diagnostics,
            document.Language);

        return new LyricsProviderResult(Name, record.Id, normalized);
    }

    private static string BuildSignatureUri(
        string endpoint,
        LyricsTrackSignature signature,
        bool requireAlbumAndDuration)
    {
        var query = new List<string>
        {
            $"track_name={Uri.EscapeDataString(signature.Title.Trim())}",
            $"artist_name={Uri.EscapeDataString(signature.Artist.Trim())}"
        };

        if (!string.IsNullOrWhiteSpace(signature.Album))
        {
            query.Add($"album_name={Uri.EscapeDataString(signature.Album.Trim())}");
        }
        else if (requireAlbumAndDuration)
        {
            throw new ArgumentException("The legacy cached LRCLIB endpoint requires an album name.", nameof(signature));
        }

        if (signature.Duration is { } duration &&
            duration.TotalSeconds is >= 1 and <= 3600)
        {
            query.Add($"duration={duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
        else if (requireAlbumAndDuration)
        {
            throw new ArgumentException("The legacy cached LRCLIB endpoint requires a valid duration.", nameof(signature));
        }

        return $"{endpoint}?{string.Join("&", query)}";
    }

    private static void ValidateSignature(LyricsTrackSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature.Artist);
    }

    private sealed class LrclibApiRecord
    {
        public LrclibApiRecord() { }
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("albumName")]
        public string? AlbumName { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("instrumental")]
        public bool? Instrumental { get; set; }

        [JsonPropertyName("hasWordSync")]
        public bool? HasWordSync { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }

        [JsonPropertyName("lyricsfile")]
        public string? Lyricsfile { get; set; }
    }
}
