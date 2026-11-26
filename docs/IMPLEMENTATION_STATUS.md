# Implementation Status

**Project:** SkyLyrics Desktop  
**Working branch:** `feat/phase-1-native-foundation`  
**Pull request:** https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/pull/1  
**Verified source commit:** `90b1fbc2afe1931a0101bd5dcb7ff748b9c6d643`

## Verified GitHub Actions run

Run [#45807615647](https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/45807615647) completed successfully on Windows x64.

- Restore: passed
- Release build: passed
- Automated tests: **17 passed, 0 failed, 0 skipped**
- Self-contained Windows x64 app publish: passed
- Executable validation: passed
- Inno Setup 7.1.1 installation: passed
- Installer compilation: passed
- Silent install smoke test: passed
- Uninstall smoke test: passed
- App, installer, and test artifacts: uploaded

### Artifact integrity

| Artifact | Size | SHA-256 |
|---|---:|---|
| `SkyLyrics.Desktop.exe` | 167,852,487 bytes | `758d28218ba3eb14b59b47bb91c6e027505f9709bf292346e9e4911b2c85e1ca` |
| `SkyLyrics-Desktop-Setup-x64.exe` | 49,365,701 bytes | `13a65ed654504219ea729ff9927d1119cbaae9b3fb179dca3469c6836f4009fd` |

The application payload is PE32+ x86-64. The Setup is an Inno Setup executable that installs only on x64 Windows and places the app in the current user's LocalAppData Programs folder. It does not require elevation. The Setup is currently unsigned.

## Implemented scope

| Area | Status |
|---|---|
| .NET 10 / WPF solution | Implemented |
| Spotify Desktop GSMTC metadata and timeline | Implemented |
| LRCLIB exact metadata lookup | Implemented |
| Conservative Spotify ↔ LRCLIB metadata validation | Implemented |
| LRC line timestamp parser | Implemented |
| Lyricsfile YAML 1.0 parser | Implemented |
| Provider-supplied word timestamps | Implemented |
| Active line and active word synchronization | Implemented |
| Animated active-line transitions | Implemented |
| Adaptive sync refresh cadence | Implemented |
| LRC / Lyricsfile / TXT local import | Implemented |
| Dark / Light theme switch | Implemented |
| SQLite offline cache with 30-day expiry | Implemented |
| Cache identity includes title/artist/album/duration | Implemented |
| Windows x64 self-contained executable | Implemented |
| Per-user Windows Setup executable | Implemented |
| Installer/uninstaller smoke test in CI | Implemented |
| LRCLIB candidate search / ambiguity selector | Not implemented |
| Floating mini-player / diagnostics screen | Not implemented |
| Code signing | Not implemented |
| Manual launch on user's PC with Spotify Desktop | Not performed |
| Figma editable screen frames | Not completed |

## Accuracy and privacy invariants

1. Plain lyrics do not acquire generated timestamps.
2. Word highlighting is enabled only from actual Lyricsfile `lines[].words` timestamps.
3. Word segments must contain text and a valid `start_ms`; malformed word timing is rejected.
4. If word sync is malformed but synced LRC is available, the app falls back to line timestamps.
5. LRCLIB metadata is checked against Spotify's title, artist, available album, and duration (2-second tolerance).
6. Cache identity includes normalized artist/title/album and rounded track duration to reduce collisions between alternate versions.
7. The app does not use Spotify private APIs or capture/record audio.

## Next implementation phases

1. LRCLIB candidate search plus safe manual selection for ambiguous results.
2. Dedicated diagnostics and floating mini-player.
3. Session lifecycle and manual validation: track switches, pause/resume, seek, reconnect, missing provider records, and offline cache.
4. Windows code signing and release hardening.
5. Figma editable screens after Figma tool availability is restored.
