using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Core.Lyrics;
using SkyLyrics.Infrastructure.Lyrics;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class LrclibClientTests
{
    [Fact]
    public async Task ExactLookupFallsBackFromLegacyCachedEndpointToDocumentedGetEndpoint()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"name\":\"TrackNotFound\",\"message\":\"No cached match\"}")
            },
            _ => JsonResponse("""
                {
                  "id": 42,
                  "trackName": "Night Drive",
                  "artistName": "Example Artist",
                  "albumName": "Midnight Album",
                  "duration": 212,
                  "instrumental": false,
                  "hasWordSync": false,
                  "plainLyrics": "Line one\nLine two",
                  "syncedLyrics": "[00:01.00]Line one\n[00:02.00]Line two",
                  "lyricsfile": ""
                }
                """));

        using var httpClient = CreateHttpClient(handler);
        var client = new LrclibClient(
            httpClient,
            new LrcParser(),
            new LyricsFileParser(),
            TimeProvider.System,
            static () => 0d);

        var result = await client.GetExactAsync(
            new LyricsTrackSignature(
                "Night Drive",
                "Example Artist",
                "Midnight Album",
                TimeSpan.FromSeconds(212)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(42, result.ProviderId);
        Assert.Equal("Night Drive", result.Document.Title);
        Assert.Equal(2, result.Document.Lines.Length);
        Assert.Equal("/api/get-cached", handler.Paths[0]);
        Assert.Equal("/api/get", handler.Paths[1]);
    }

    [Fact]
    public async Task SearchReturnsStructuredCandidateRecords()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""
            [
              {
                "id": 7,
                "trackName": "Night Drive",
                "artistName": "Example Artist",
                "albumName": "Midnight Album",
                "duration": 212,
                "instrumental": false,
                "plainLyrics": "Unsynced source text",
                "syncedLyrics": null,
                "lyricsfile": ""
              }
            ]
            """));

        using var httpClient = CreateHttpClient(handler);
        var client = new LrclibClient(
            httpClient,
            new LrcParser(),
            new LyricsFileParser(),
            TimeProvider.System,
            static () => 0d);

        var result = await client.SearchAsync(
            new LyricsTrackSignature("Night Drive", "Example Artist", null, TimeSpan.FromSeconds(212)),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal(7, candidate.ProviderId);
        Assert.Equal("Unsynced source text", candidate.Document.PlainText);
        Assert.Equal(LyricsSyncMode.Unsynced, candidate.Document.SyncMode);
    }

    [Fact]
    public async Task ExactLookupUsesLyricsfileForAuthenticWordTiming()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""
            {
              "id": 13,
              "trackName": "Timed Song",
              "artistName": "Singer",
              "albumName": "Album",
              "duration": 10,
              "instrumental": false,
              "lyricsfile": "version: \"1.0\"\nmetadata:\n  title: Timed Song\n  artist: Singer\nlines:\n  - text: \"Hello world\"\n    start_ms: 1000\n    words:\n      - text: \"Hello \"\n        start_ms: 1000\n      - text: world\n        start_ms: 1600\n"
            }
            """));

        using var httpClient = CreateHttpClient(handler);
        var client = new LrclibClient(
            httpClient,
            new LrcParser(),
            new LyricsFileParser(),
            TimeProvider.System,
            static () => 0d);

        var result = await client.GetExactAsync(
            new LyricsTrackSignature("Timed Song", "Singer", "Album", TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LyricsSyncMode.WordSynced, result.Document.SyncMode);
        Assert.Equal(2, result.Document.Lines[0].Words.Length);
    }

    [Fact]
    public async Task RespectsRetryAfterAndRetriesRateLimitedRequest()
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            if (call == 1)
            {
                var limited = new HttpResponseMessage((HttpStatusCode)429);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }

            return JsonResponse("""
                {
                  "id": 3,
                  "trackName": "Track",
                  "artistName": "Artist",
                  "duration": 30,
                  "instrumental": false,
                  "plainLyrics": "Text"
                }
                """);
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new LrclibClient(
            httpClient,
            new LrcParser(),
            new LyricsFileParser(),
            TimeProvider.System,
            static () => 0d);

        var result = await client.GetExactAsync(
            new LyricsTrackSignature("Track", "Artist", null, TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, call);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("https://lrclib.net/"),
            Timeout = TimeSpan.FromSeconds(5)
        };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        : HttpMessageHandler
    {
        private int _index;

        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            var current = Interlocked.Increment(ref _index) - 1;
            var factory = responses[Math.Min(current, responses.Length - 1)];
            return Task.FromResult(factory(request));
        }
    }
}
