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

## Verified in source at baseline

- MainWindow uses WPF native window chrome with CanResize and SingleBorderWindow.
- F11 toggles borderless fullscreen; Escape restores the previous window state.
- Main application close is not converted into a hide-to-tray action.
- Focus, Mini Player and Game Mode share the same ShellViewModel/SceneState.
- Game Mode already supports position persistence, resize handles, topmost/pin, settings and close.
- Spotify is the only playback source currently selected by the GSMTC monitor.
- Discord Rich Presence uses local Discord IPC.
- OBS output is loopback-only.

## Not yet implemented in this preview

- YouTube Music session routing/source selection.
- General Windows Media and other GSMTC source selection.
- Dual-language lyric documents and translation workflow.
- In-app lyric editor and timestamp calibration.
- Extreme Visual Engine with optional real audio-session input.
- Click-through and expanded Advanced Game Mode behavior.
- Full Customization Studio and all 2.1 navigation sections.

These are planned workstreams, not active controls or completed features.

## Phase tracking

| Workstream | Status |
|---|---|
| Baseline selection and isolated work branch | Complete |
| 2.1 preview version identity and release metadata | In progress |
| Window chrome and window-state QA | Pending |
| Multi-source playback adapters | Pending |
| Advanced Lyrics Studio | Pending |
| Extreme Visual Engine | Pending |
| Advanced Game Mode | Pending |
| Integrations and Customization Studio | Pending |
| Windows device acceptance and release | Pending |

## Release gates

A workstream is complete only after implementation, automated tests, successful Windows CI, matching documentation and the required manual acceptance. A visible control without a working operation is not accepted. A connector must never show Connected without a real handshake or a verified media-session match.
