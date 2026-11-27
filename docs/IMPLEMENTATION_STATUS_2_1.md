# SkyLyrics Desktop 2.1 — Implementation Status

**Release line:** 2.1.0-preview.1  
**Working branch:** feature/skylyrics-2.1-build  
**Baseline commit:** 147a482509259b9c839ef08da405a776d371f3b7  
**Target:** Windows 11 x64 · C# 14 · .NET 10 · WPF

## Baseline verification

The selected baseline contains the WPF desktop application, Core/Application/Infrastructure projects, unit tests, the Spotify GSMTC adapter, LRCLIB integration, SQLite cache, Discord Rich Presence, OBS loopback output, Focus/Karaoke, Mini Player and Game Mode.

The baseline commit's Windows CI run #58 completed successfully. Its recorded steps include restore, Release build, unit tests, self-contained win-x64 publish, Inno Setup compilation, installer size/checksum verification and artifact upload:

https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/46020245113

This is baseline CI evidence only. It is not a claim that the application has been manually validated on the user's device or that future 2.1 features are implemented.

## Verified in the current working branch

- MainWindow uses WPF native window chrome with CanResize and SingleBorderWindow.
- F11 toggles borderless fullscreen; Escape restores the previous window state.
- The fullscreen toggle updates its named label without replacing the button's icon or content tree.
- Main application close is not converted into a hide-to-tray action.
- Focus, Mini Player and Game Mode share the same ShellViewModel/SceneState.
- Game Mode already supports position persistence, resize handles, topmost/pin, settings and close.
- The GSMTC monitor recognizes exact Spotify identities, known Windows Media player identities and known browser media-session identities.
- Settings exposes an Automatic or selected detected media-session source and persists the choice. Browser sessions are still not proof that YouTube Music is the website.
- Discord Rich Presence uses local Discord IPC.
- OBS output is loopback-only.

## Not yet implemented in this preview

- Explicit browser-site confirmation for YouTube Music; browser selection identifies Chrome/Edge, not the website.
- Additional unrecognized GSMTC application identities beyond the exact supported identity catalog.
- Dual-language lyric documents, translation workflow and timestamp calibration.
- Extreme Visual Engine with optional real audio-session input.
- Click-through and expanded Advanced Game Mode behavior.
- Full Customization Studio and all 2.1 navigation sections.

These are planned workstreams, not active controls or completed features.

## Phase tracking

| Workstream | Status |
|---|---|
| Baseline selection and isolated work branch | Complete |
| 2.1 preview version identity and release metadata | Complete; build, test and package CI passed |
| Window chrome and window-state QA | Native chrome retained and fullscreen button code fixed; manual device acceptance pending |
| Multi-source GSMTC recognition and playback adapter | Implemented; Windows CI run 46029075732 passed; device acceptance pending |
| Detected media-session source picker and saved selection | Implemented; awaiting Windows CI and device acceptance |
| Explicit YouTube Music website/source confirmation | Pending |
| LRC editor, validation and export | Implemented in the working branch; CI and device acceptance pending |
| Dual-language lyrics, translation and word-level editing | Pending |
| Extreme Visual Engine | Pending |
| Advanced Game Mode | Pending |
| Integrations and Customization Studio | Pending |
| Windows device acceptance and release | Pending |

The LRC editor exports line-synced and unsynced documents. It deliberately blocks word-synced documents in this editor because the standard LRC format cannot safely preserve per-word timestamps.

## Release gates

A workstream is complete only after implementation, automated tests, successful Windows CI, matching documentation and the required manual acceptance. A visible control without a working operation is not accepted. A connector must never show Connected without a real handshake or a verified media-session match.

## Verified preview artifact

- Windows CI run: https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/46029075732
- Artifact: `skylyrics-windows-x64-installer`
- Installer size: 78,671,364 bytes
- SHA-256: `7bf079a5b1df1d304947a20cc8dfeb9c25aa62a669d035ffd186a92eaedafc8d`
- The downloaded installer checksum was recomputed locally and matched the checksum file bundled in the artifact.
- This confirms package integrity; installation and interactive behavior still require manual acceptance on Windows 11.
