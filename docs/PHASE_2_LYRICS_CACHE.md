# Phase 2 — LRCLIB, LRC/Lyricsfile Parsing and SQLite Cache

## Scope

This phase adds LRCLIB exact lookup and manual search, local LRC/Lyricsfile import, a source-preserving lyrics document model, and a compressed SQLite cache. It does not add the event-driven SyncScheduler, current-line selection, cinematic rendering, OBS or Discord.

## LRCLIB API behavior

- Exact metadata lookup uses `GET /api/get` with title and artist; album and duration are supplied when available.
- The client attempts the legacy `GET /api/get-cached` route only when the complete signature is available, then falls back to the currently documented `GET /api/get` route on a miss or unavailable endpoint.
- Manual search uses `GET /api/search`; results are ranked for presentation but are not silently accepted as exact matches.
- A selected result can be fetched by provider ID with `GET /api/get/{id}`.
- The client identifies itself with a product User-Agent, sends requests sequentially with a 300 ms minimum interval, and honors `Retry-After` on HTTP 429 with bounded retries and jitter.
- The current official v1 API documentation lists `/api/get` and `/api/search`. The cached endpoint is retained only as a compatibility attempt because it is not listed in the current v1 reference.

## Parsing and truthfulness

- LRC supports metadata tags, multiple timestamps on one source line, minute/second and hour/minute/second forms, fractional seconds, and the LRC offset tag.
- Lyricsfile parser supports only version 1.0. It rejects unsupported versions, invalid timestamps, missing required metadata, malformed YAML and duplicate keys; YAML recursion and file size are bounded.
- Lyricsfile line overlaps are preserved. Input lines are sorted by their actual source timestamp for the runtime document; no line end is inferred.
- Lyricsfile word timings are retained only when provided. Word text is not rewritten to make it match a line.
- Lyricsfile `offset_ms` is not applied because the draft specification says its semantics are unresolved.
- Plain lyrics are represented as `Unsynced`; no timing or word timing is generated.
- A 2 MiB local import limit prevents unexpectedly large lyric files from consuming unbounded memory.

## SQLite cache

- Database path: `%LOCALAPPDATA%\SkyLyrics\lyrics-cache.db`.
- Journal mode is WAL, synchronous mode is NORMAL, and busy timeout is five seconds.
- Lyrics documents are JSON-serialized and GZip-compressed before storage.
- Cache entries are upserted by a SHA-256 key derived from normalized title, artist, album and rounded duration. Recent access timestamps are recorded. Eviction removes least-recently-used entries first; the compressed payload target is 180 MiB, reserving space for SQLite indexes and journaling, while the combined database, WAL and SHM files are measured against a 200 MiB ceiling. The database is checkpointed and compacted after eviction.
- Cache lookup failures do not prevent an LRCLIB lookup; cache write failures do not discard lyrics already retrieved from the provider.

## Desktop behavior

- Track changes trigger an exact lyrics lookup.
- A local cache hit is used before the network provider.
- A generation counter and cancellation token prevent stale responses from replacing lyrics for a newer track.
- The main window labels the document as Word-Synced, Line-Synced, Unsynced, or Instrumental.
- The Import Lyrics action accepts `.lrc`, `.yaml`, `.yml`, and `.lyricsfile.yaml`; a correctly identified local file can be cached using its own metadata.

## Automated validation

Tests cover LRC formats and offsets, Lyricsfile structure and safety, overlapping lines, candidate ranking, legacy API fallback, rate-limit retry, service cache behavior, and SQLite round-trip/upsert.

A live LRCLIB lookup requires a network connection and remains separate from deterministic CI tests. Spotify live-session behavior also requires a manual Windows 11 test.

## Exit criteria

- [x] Windows x64 build and all unit tests pass (68/68 tests; 0 compiler warnings and 0 errors).
- [x] LRCLIB API contract and graceful cached-endpoint fallback are implemented.
- [x] LRC and Lyricsfile parsing preserve only source-supplied timestamps.
- [x] SQLite WAL cache with compressed payloads is implemented.
- [x] Desktop exact lookup, stale-request cancellation and local import are wired.
- [ ] Manual online lookup and local-file import are verified on Windows 11.

## Next phase

Phase 3 implements the event-driven SyncScheduler, binary-search line selection, playback-seek rescheduling and immutable SceneState publication. It consumes the LyricsDocument and PlaybackSnapshot produced by earlier phases.
