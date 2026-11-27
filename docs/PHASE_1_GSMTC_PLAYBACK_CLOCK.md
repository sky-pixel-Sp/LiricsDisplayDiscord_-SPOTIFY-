# Phase 1 — GSMTC Adapter and PlaybackClock

## Scope

This phase adds Spotify Desktop media-session discovery, metadata and timeline updates, and monotonic playback-position interpolation. Lyrics retrieval, lyric parsing, SQLite, scheduling, cinematic rendering, OBS and Discord remain out of scope.

## Windows integration

- `SkyLyrics.Infrastructure.Windows` is the Windows-only infrastructure project and targets `net10.0-windows10.0.19041.0`.
- The project uses the Windows SDK projections selected by its Windows-specific target framework. No third-party Spotify API or private endpoint is used.
- `SpotifyGsmTcSessionMonitor` obtains the session manager with `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`.
- The monitor listens for `SessionsChanged`, `CurrentSessionChanged`, `MediaPropertiesChanged`, `PlaybackInfoChanged`, and `TimelinePropertiesChanged`.
- Session selection uses an explicit case-insensitive allowlist: `Spotify.exe`, `SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`, and `SpotifyAB.SpotifyMusic_zpdnekdrzrea0`. Browser app identities are not fuzzy-matched. Additional identities can be injected into `SpotifyAppIdentityMatcher` when a specific installation reports a different Windows AUMID.
- Event callbacks are serialized by a refresh gate. Old session event handlers are detached when selection changes or the service stops.

## PlaybackClock

- The clock uses `TimeProvider.GetTimestamp()` and `GetElapsedTime()` for monotonic interpolation. The system implementation is based on the high-resolution monotonic Stopwatch clock.
- When a GSMTC timeline observation arrives, its source position is corrected by the positive age of `LastUpdatedTime` multiplied by the reported playback rate.
- Further interpolation uses only the monotonic timestamp captured when the observation is applied. It does not repeatedly add the wall-clock age.
- Paused, stopped and unknown states do not advance.
- Pause/resume and rate transitions preserve the current interpolated position.
- Seek observations re-anchor immediately. Start and end timeline bounds are enforced.
- Invalid or missing playback rates normalize to 1.0; a future source timestamp is not used to move playback backwards.

## User-visible behavior

The WPF shell displays the detected session state, supplied title/artist/album values, playback state, source AUMID, and the current timeline position. No lyrics are displayed in this phase.

## Validation

The unit tests use an injected manual TimeProvider; no sleeps or real-time timing assumptions are used. Windows CI compiles the GSMTC infrastructure project and runs the deterministic tests.

A real-device validation still requires Spotify Desktop to be installed and active on a Windows 11 machine. Confirm the session source AUMID if it is not one of the default identities, and add that exact AUMID to the matcher configuration. Validate track changes, pause/resume, seeking, and Spotify exit/restart.

## Exit criteria

- [x] Windows-specific GSMTC adapter and session lifecycle are implemented.
- [x] Explicit Spotify Desktop identity matching is implemented.
- [x] Monotonic clock supports rate, pause, resume, seek and timeline bounds.
- [x] Automated tests cover playback-clock arithmetic and identity matching.
- [x] Windows CI passes for this branch (31/31 tests passed, 0 compiler warnings/errors).
- [ ] Manual Spotify Desktop validation on Windows 11 is recorded.

## Next phase

Phase 2 adds LRCLIB exact lookup/search, LRC and Lyricsfile parsing, and SQLite caching. It consumes the immutable playback snapshot and does not change the GSMTC adapter.
