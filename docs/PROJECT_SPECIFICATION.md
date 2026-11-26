# SkyLyrics Desktop — Product Specification

**Status:** Locked  
**Target:** Windows 11 x64  
**Product type:** Native desktop companion for Spotify Desktop

## 1. Product objective

Display the lyrics for the track currently playing in Spotify Desktop, match the correct recording, and synchronize the visible lyric line and (where reliable word timestamps exist) individual words to playback position. Provide polished animated presentation without capturing, recording, or analyzing the audio stream.

## 2. Locked configuration

| Area | Decision |
|---|---|
| Product name | SkyLyrics Desktop |
| Platform | Windows 11 x64 |
| Language | C# |
| Runtime/UI | .NET 10 LTS, WPF |
| Spotify integration | Windows GSMTC media session |
| Playback source | Spotify Desktop only |
| Lyrics provider | LRCLIB API |
| Local lyrics | User-selected/imported LRC files |
| Cache | SQLite |
| Synchronization | Line-level and word-level when timestamp data exists |
| Themes | Dark and Light |
| Delivery | Signed-ready Windows x64 installer package; signing certificate is a release prerequisite, not assumed to exist |
| Repository | sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY- |

## 3. Product principles

1. Correct track matching is more important than returning a fast but incorrect lyric result.
2. Never invent lyric text, translations, timestamps, artist names, album metadata, or artwork.
3. Always distinguish synchronized lyrics, unsynchronized lyrics, local lyrics, and unavailable lyrics.
4. Do not scrape Spotify's private endpoints or bypass Spotify authentication/DRM.
5. Do not capture or record Spotify audio for lyric recognition.
6. The application must remain useful when the lyrics provider is offline: show cached and user-imported lyrics.
7. All network access must have bounded timeouts, cancellation, and visible failure states.
8. User playback controls are not required for the first release; Spotify remains the playback controller.

## 4. Functional requirements

### FR-01 — Spotify session detection
- Enumerate current Windows GSMTC sessions.
- Select the Spotify Desktop session by verified source application identity/package metadata.
- Handle Spotify starting after SkyLyrics, closing, reconnecting, and switching tracks.
- Never silently substitute metadata from another media player.
- Display a clear disconnected state when no Spotify session is available.

### FR-02 — Track metadata
Read title, artist, album, artwork thumbnail where exposed, playback status, and playback timeline from the media session. Normalize whitespace and metadata for search without modifying the displayed original values.

### FR-03 — Candidate lyric lookup
- Query LRCLIB using title, artist, album, and duration where available.
- Prefer exact metadata matches and duration-consistent recordings.
- Rank candidates deterministically.
- Reject ambiguous candidates rather than silently showing a likely-wrong lyric.
- Provide a manual candidate selection path when multiple credible matches remain.
- Cache positive and negative results with separate expiration policies.

### FR-04 — Lyric formats and provenance
- Parse LRC line timestamps.
- Parse enhanced LRC word/segment timestamps only where the returned format explicitly provides them.
- Preserve original text and timestamps.
- Store provider/source, retrieval time, track metadata used for matching, and sync capability.
- Plain text lyrics must be labelled Unsynced; never synthesize timestamps.

### FR-05 — Synchronization engine
- Use the media session timeline as the playback clock.
- Use a monotonic local clock to interpolate between timeline observations only while playback is confirmed Playing.
- Re-anchor on every timeline update, seek, pause, resume, track change, and session reconnect.
- Pause freezes the active lyric position.
- Seek recomputes the active lyric immediately.
- Clamp interpolation to the media timeline and never extrapolate beyond the known track duration.
- Apply a user-configurable offset, default 0 ms, with a bounded range of -500 ms to +500 ms.
- Line-level mode activates the most recent timestamped line at or before the effective playback position.
- Word-level mode highlights a word only if the source supplies word-level timestamps; otherwise degrade gracefully to line-level and disclose the reason.

### FR-06 — Animated UI
- Smooth line transition and active-line emphasis.
- Word highlight progression for word-timed lyrics.
- Auto-scroll with reduced-motion option.
- Album artwork presentation and restrained color-derived ambience.
- No animation may change the underlying lyric timestamp or synchronization calculation.

### FR-07 — Views
- Main Now Playing dashboard.
- Lyrics Focus / karaoke view.
- Floating mini player with always-on-top option.
- Settings.
- Lyrics diagnostics / source details.

### FR-08 — Themes and accessibility
- Dark and Light themes.
- Respect Windows text scaling where practical.
- Keyboard navigation and visible focus states.
- Contrast-safe active lyric state.
- Reduced motion and animation intensity setting.
- No information conveyed by color alone.

### FR-09 — Local files and offline
- Import user-selected .lrc files.
- Associate local files with a track through explicit user selection or deterministic metadata matching.
- Cache lyrics and metadata in SQLite.
- Do not crawl arbitrary folders or upload local files.
- Offline mode uses cache and user-imported lyrics only.

## 5. Non-functional requirements

- Target idle memory budget: under 200 MB on the reference laptop; measure rather than assume.
- No continuous audio capture or DSP pipeline.
- Network requests must be cancellable and use bounded timeouts.
- UI thread must not perform network or database operations.
- Database writes must be transactional.
- Application must recover from provider failures and Spotify session loss.
- Logs must not contain access tokens, private file contents, or unnecessary personal data.
- Installer must support clean install, upgrade, and uninstall.
- CI must produce a Windows x64 artifact and publish a checksum.

## 6. Explicit non-goals for v1

- Spotify Web Player support.
- Spotify playback control.
- Audio fingerprinting or microphone/audio loopback capture.
- Automatic translation or AI-generated lyrics.
- Mobile, macOS, or Linux support.
- Discord Rich Presence integration unless separately approved after the core product is stable.

## 7. User-visible states

| State | Required UI behavior |
|---|---|
| Spotify not detected | Explain how to start Spotify Desktop; do not show stale track data as current |
| Loading lyrics | Keep current track metadata visible and show lookup progress |
| Synced lyrics | Show timestamped line and optional word highlighting |
| Unsynced lyrics | Show text with explicit Unsynced label |
| Multiple matches | Ask user to select a candidate |
| No lyrics found | Explain no verified match was found; offer local LRC import |
| Provider unavailable | Show cached result if available and disclose offline/stale status |
| Session paused | Freeze lyric position |
| Seeking | Recalculate immediately after timeline update |
| Track changed | Cancel old lookup and ignore stale response |

## 8. Definition of done

A release candidate is acceptable only when:
- Spotify Desktop session detection works on a clean Windows 11 x64 machine.
- Track changes, pause/resume, seek, session disconnect/reconnect are tested.
- Correct, incorrect, ambiguous, synced, unsynced, and missing-lyrics cases are tested.
- Word highlighting is enabled only for actual word-timestamp data.
- Dark and Light themes pass UI checks.
- No blocking network/database work runs on the UI thread.
- Automated tests pass.
- Installer installs, launches, upgrades, and uninstalls successfully.
- CI artifact exists, has a recorded size and SHA-256, and has been downloaded and inspected.
