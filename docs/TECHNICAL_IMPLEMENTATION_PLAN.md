# SkyLyrics Desktop — Technical Implementation Plan

**Status:** Architecture baseline  
**Target:** Windows 11 x64, .NET 10 LTS, WPF

## 1. Architecture overview

Use a modular, single-process WPF desktop application for v1. Keep OS integration, provider access, synchronization, persistence, and presentation behind interfaces so each subsystem can be tested independently.

```text
SkyLyrics.Desktop (WPF shell)
  ├── Presentation
  │    ├── Now Playing / Lyrics / Karaoke / Mini Player / Settings
  │    └── ViewModels + UI animation states
  ├── Application
  │    ├── PlaybackOrchestrator
  │    ├── TrackMatchingService
  │    ├── LyricsResolutionService
  │    └── LyricsSyncCoordinator
  ├── Domain
  │    ├── TrackMetadata / PlaybackSnapshot
  │    ├── LyricsDocument / TimedLine / TimedWord
  │    ├── MatchConfidence / LyricsProvenance
  │    └── PlaybackState
  ├── Infrastructure
  │    ├── WindowsMediaSession (GSMTC)
  │    ├── LrclibClient (HttpClient)
  │    ├── SqliteLyricsCache
  │    ├── LrcParser / LyricsfileParser
  │    └── LocalLrcImporter
  └── Diagnostics
       ├── Structured logging
       └── Sync diagnostics / provider status
```

The UI must depend on application/domain abstractions, not directly on Windows APIs or HTTP responses.

## 2. Solution and project layout

Create this solution after the architecture baseline is approved:

```text
SkyLyrics.sln
src/
  SkyLyrics.Desktop/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    Views/
      NowPlayingView.xaml
      LyricsFocusView.xaml
      FloatingPlayerWindow.xaml
      SettingsView.xaml
      LyricsDiagnosticsView.xaml
    ViewModels/
    Themes/
      Dark.xaml
      Light.xaml
    Assets/
  SkyLyrics.Application/
    Abstractions/
    Playback/
    Lyrics/
    Synchronization/
  SkyLyrics.Domain/
    Models/
    Matching/
    Parsing/
  SkyLyrics.Infrastructure.Windows/
    MediaSession/
    Artwork/
  SkyLyrics.Infrastructure.Lyrics/
    Lrclib/
    Parsing/
    Import/
  SkyLyrics.Infrastructure.Persistence/
    Sqlite/
tests/
  SkyLyrics.Domain.Tests/
  SkyLyrics.Application.Tests/
  SkyLyrics.Infrastructure.Tests/
  SkyLyrics.Desktop.Tests/
installer/
  windows/
docs/
.github/
  workflows/
```

Use dependency injection in the desktop composition root. Keep Windows-specific GSMTC types inside Infrastructure.Windows and map them to domain-neutral records.

## 3. Windows media session adapter

### API
Use Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.

### Startup and lifecycle
1. Request the session manager asynchronously.
2. Subscribe to SessionsChanged and CurrentSessionChanged.
3. Enumerate sessions and identify Spotify Desktop using source application identity metadata; do not assume the current system session is Spotify.
4. Subscribe to MediaPropertiesChanged, PlaybackInfoChanged, and TimelinePropertiesChanged on the selected Spotify session.
5. Unsubscribe from the previous session before switching.
6. Re-enumerate after session changes and tolerate unavailable metadata.
7. On application shutdown, detach event handlers and release references.

### Data mapping
Map the session into:
- Track title, artist, album, album artwork reference/thumbnail if available.
- Playback status.
- Timeline start, end, position, last updated time, and playback rate when exposed.

Do not use Spotify's private APIs or scrape its internal network traffic. Do not request global audio capture permissions.

### Timing
The session timeline is authoritative at each observation. While Playing, interpolate position using a monotonic clock between observations. Re-anchor on every timeline update and state change. While Paused, freeze. When Stopped or session lost, do not advance.

GSMTC may not emit a continuous event for every frame of playback. Use a modest UI/sync refresh cadence (for example 8–10 Hz) only while a timestamped lyric is visible; do not issue network calls on this cadence. Measure timing error on real Spotify sessions and make the cadence configurable internally.

## 4. Track matching and lyric resolution

### Provider contract
Define ILyricsProvider with methods for exact signature lookup, candidate search, and retrieval by provider ID. The first provider is LRCLIB.

### Request strategy
- Prefer stable v1 GET /api/get using track_name and artist_name; include album_name and duration whenever available.
- If exact lookup returns no result or ambiguity remains, use v1 GET /api/search for candidate discovery.
- Treat the v2 search endpoint as optional/experimental only; do not make v1 release correctness depend on a beta endpoint.
- Retrieve the chosen candidate by ID when appropriate.
- Send an identifying User-Agent containing product name, version, and repository URL.
- Send requests sequentially; honor Retry-After for 429/503 and apply jitter.
- Use cancellation tokens so a track change cancels the prior lookup.
- Do not retry 404 as a transient error.

### Match scoring
Use deterministic normalized comparisons:
- Track title: exact normalized match receives strongest weight.
- Artist: exact normalized artist match receives strongest weight.
- Album: supporting signal; tolerate absent album metadata but penalize conflicting album values.
- Duration: strongest disambiguation signal when available; LRCLIB exact lookup documents a ±2 second duration tolerance.
- Explicit/version suffixes such as live, acoustic, remaster, radio edit, karaoke, instrumental must be preserved as meaningful distinctions.
- If top candidates are too close, require user selection rather than guessing.

Do not claim a universal numeric confidence percentage until a labelled evaluation dataset exists. Initially expose a reasoned match status: Exact, Strong, Ambiguous, or No Match.

### Lyrics provenance
Persist provider name, provider record ID, returned track metadata, fetched timestamp, whether line timing exists, whether word timing exists, and cache age. Display source and sync capability in Diagnostics.

## 5. Parsing model

Domain model:
- LyricsDocument: metadata, source, sync capability, lines.
- TimedLine: start time, text, optional end time, ordered words.
- TimedWord: start time, optional end time, text.
- LyricsSyncCapability: None, Line, Word.
- LyricsSource: LRCLIB, LocalFile, Cache.

Parser requirements:
- Parse LRC timestamps including minute/second/fraction variants supported by the provider.
- Support multiple timestamps attached to one line.
- Ignore malformed timestamp records safely and report diagnostics.
- Preserve blank lines where useful for stanza spacing.
- Parse word-level data only from the documented Lyricsfile structure when hasWordSync is true and validate the parsed structure.
- Never derive word timing by splitting a line evenly.
- Reject non-monotonic or impossible timestamps from word-level rendering; retain safe line-level data if valid.
- Use invariant culture and integer/tick arithmetic internally to avoid floating-point drift.

## 6. Synchronization coordinator

Input: PlaybackSnapshot + LyricsDocument + user offset.

EffectivePosition = authoritative/interpolated playback position + user offset.

Line mode:
- Find the latest line whose timestamp is <= EffectivePosition.
- Before the first timestamp, show the upcoming state without falsely marking a line active.
- On seek, recompute immediately.
- On pause, freeze.
- On track change, clear old active state before resolving new lyrics.

Word mode:
- Enabled only when document capability is Word.
- Select the active word using its own timestamps.
- If word data is absent or invalid, fall back to line mode and show a capability notice.
- Keep line activation and word activation separate so the UI does not alter timing.

Avoid UI animation timers as the source of truth. The synchronization coordinator emits state; the view animates transitions between states.

## 7. Persistence and cache

SQLite tables:
- TrackIdentity: normalized title/artist/album/duration signature.
- LyricsRecord: provider ID, raw plain/synced/Lyricsfile payload, sync capability, provenance.
- CacheEntry: key, fetched time, expiry, last status.
- UserSettings: theme, offset, reduced motion, animation intensity, floating window preference.
- LocalLyricsFile: user-selected path metadata and file fingerprint (never upload file contents).

Use parameterized queries, transactions, schema versioning, and a single serialized write queue. Keep cache expiry policy separate for successful records, not-found responses, and transient failures.

## 8. UI and animation implementation

WPF views use MVVM. Bind presentation to immutable or observable view state. Keep all animations in presentation; they must not feed back into synchronization.

Main dashboard:
- Left navigation rail.
- Track header with artwork, title, artist, album.
- Center lyrics column with previous/current/next lines.
- Bottom playback progress and session status.
- Source/sync capability indicator.

Lyrics Focus:
- Large centered lyric typography.
- Active line emphasized; previous and next lines reduced opacity.
- Optional word highlight when word timing exists.
- Fullscreen exit and reduced-motion support.

Floating player:
- Compact window, always-on-top toggle, drag region, resize constraints.
- No hidden playback control behavior.

Settings:
- Appearance: Dark/Light/System.
- Synchronization: offset -500..+500 ms.
- Animation: intensity, reduced motion.
- Startup behavior and floating window preference.
- Diagnostics and cache clear action.

Animation rules:
- Animate opacity/translation/scale only; never animate timestamps.
- Use short, interruptible transitions.
- Avoid expensive blur/shader effects on the reference Intel UHD laptop.
- Respect system reduced-motion preference and user override.

## 9. Error handling and diagnostics

Define typed outcomes rather than throwing provider errors into the UI:
- SpotifyUnavailable
- TrackMetadataIncomplete
- LyricsNotFound
- LyricsAmbiguous
- ProviderRateLimited
- ProviderUnavailable
- LyricsMalformed
- LocalFileInvalid

Diagnostics should expose current session source, last metadata update, track identity, provider request outcome, selected lyric record, sync capability, current offset, and last timeline re-anchor. Redact private paths and never log lyric text by default.

## 10. Test strategy

### Domain unit tests
- LRC parsing: valid, malformed, multiple timestamps, fractions, blank lines.
- Word timing parsing: valid, missing, malformed, non-monotonic.
- Track normalization and version suffix handling.
- Candidate ranking and ambiguity behavior.
- Line selection at exact boundary, before first line, after final line, offset boundaries.
- Word selection and line fallback.

### Application tests
- Track change cancels prior lookup.
- Out-of-order provider responses cannot overwrite the current track.
- Pause freezes position.
- Resume re-anchors.
- Seek updates active line immediately.
- Session loss clears stale playing state.
- Retry-After is honored.
- Cache hit avoids a provider request.

### Windows integration tests
- Enumerate media sessions.
- Select Spotify among multiple media apps.
- Receive metadata/playback/timeline changes.
- Reconnect after Spotify restart.
- Validate artwork retrieval and disposal behavior.

### UI tests
- Dark and Light theme snapshots.
- 100%, 125%, 150% Windows scaling.
- Window resizing and minimum dimensions.
- Floating window always-on-top and focus behavior.
- Reduced motion.

### Release gates
- Build Release x64 on Windows.
- Run all tests.
- Publish installer.
- Install and launch on clean Windows 11 test machine.
- Upgrade from previous package and uninstall.
- Record artifact size and SHA-256.
- Download and inspect the exact Actions artifact before claiming delivery.

## 11. CI/CD plan

After source projects exist, add GitHub Actions workflow triggered by push to main, pull requests, and manual dispatch:
1. Checkout.
2. Install .NET 10 SDK.
3. Restore.
4. Build Release x64.
5. Run tests with TRX results.
6. Publish self-contained or framework-dependent package based on measured installer size and runtime requirements.
7. Build installer using a maintained Windows installer toolchain.
8. Upload installer and test reports as workflow artifacts.
9. Generate SHA-256 checksum and include it in release metadata.

Do not create a green-looking placeholder workflow before the solution exists. The workflow must fail if expected solution, tests, or installer output are missing.

## 12. Implementation phases and exit criteria

### Phase 0 — Repository baseline
- Product and technical documents committed.
- Branch policy and issue tracking established.
- Exit: architecture and UI scope reviewed.

### Phase 1 — Solution skeleton
- Create solution, projects, DI, logging, configuration, app shell.
- Exit: clean Windows x64 build and startup.

### Phase 2 — Spotify session adapter
- GSMTC discovery, identity filtering, metadata, playback and timeline events.
- Exit: integration test confirms Spotify metadata and pause/seek/track-change behavior.

### Phase 3 — Lyrics provider and parser
- LRCLIB client, rate limiting, exact lookup, search, LRC and Lyricsfile parsing.
- Exit: fixture-based parser tests and real provider contract tests pass.

### Phase 4 — Synchronization
- Playback interpolation, re-anchor, line and word state.
- Exit: measured synchronization error recorded across multiple tracks and seeks.

### Phase 5 — UI and animation
- Main, focus, floating, settings, diagnostics; both themes.
- Exit: accessibility, scaling, reduced-motion, and visual review pass.

### Phase 6 — Persistence and resilience
- SQLite cache, offline behavior, import/export of local LRC.
- Exit: provider-offline and restart tests pass.

### Phase 7 — Installer and release
- Installer, CI, upgrade/uninstall, artifact verification.
- Exit: exact installer downloaded, hash recorded, and clean-machine smoke test passed.

## 13. External technical references

- LRCLIB API documentation: https://lrclib.net/docs
- Windows GSMTC session manager: https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager
- Windows GSMTC session: https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession
- Windows GSMTC timeline properties: https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties
