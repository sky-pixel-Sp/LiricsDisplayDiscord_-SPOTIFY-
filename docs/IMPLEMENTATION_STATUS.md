# Implementation Status

**Project:** SkyLyrics Desktop  
**Working branch:** `feat/phase-1-native-foundation`  
**PR:** https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/pull/1

## Verified implementation

At source commit `f0b42eec14125e54ef97157e735911d629a0853e`, GitHub Actions run #6 completed successfully:

- Windows x64 restore: passed
- Release solution build: passed
- Automated tests: 7 passed, 0 failed, 0 skipped
- Self-contained Windows x64 publish: passed
- Executable existence and non-zero size validation: passed
- Build artifact upload: passed
- Test-result artifact upload: passed

The published file was a PE32+ x86-64 Windows GUI executable. CI reported 165,558,220 bytes before ZIP compression.

## Implemented scope

| Area | Status |
|---|---|
| .NET 10 / WPF solution | Implemented |
| Spotify Desktop GSMTC metadata + timeline | Implemented |
| LRCLIB exact lookup | Implemented |
| LRC line timestamp parser | Implemented |
| Lyricsfile YAML 1.0 parser | Implemented |
| Provider-supplied word timestamps | Implemented |
| Active line / word synchronization engine | Implemented |
| Animated active-line transitions | Implemented |
| LRC / Lyricsfile / TXT local import | Implemented |
| Dark / Light theme switch | Implemented |
| SQLite cache | Not implemented |
| Ambiguous-match selection UI | Not implemented |
| LRCLIB search fallback | Not implemented |
| Floating mini-player / diagnostics screen | Not implemented |
| Signed installer | Not implemented |
| Manual app launch test on user's PC | Not performed |
| Figma editable design frames | Not completed |

## Accuracy invariants

1. Plain lyrics do not acquire generated timestamps.
2. Word highlighting is enabled only from actual Lyricsfile `lines[].words` timestamps.
3. Word segments must contain text and a valid `start_ms`; invalid word timing is rejected.
4. If a Lyricsfile with invalid word timing has valid `syncedLyrics`, the app falls back to those line timestamps.
5. If the source has no timestamps, the UI labels the lyrics UNSYNCED.
6. The app does not use Spotify private APIs or capture/record audio.

## Next implementation phases

1. SQLite cache with a stable track identity key, cache freshness, and offline retrieval.
2. LRCLIB candidate search and ambiguity handling.
3. Session lifecycle hardening: current-session changes, track seeks, pause/resume, reconnect, and stale-request cancellation.
4. Mini-player, settings, and diagnostics views.
5. Windows installer packaging, uninstall verification, app metadata, icon, and optional signing.
6. Manual validation on Windows with Spotify Desktop and a broad set of real tracks.
