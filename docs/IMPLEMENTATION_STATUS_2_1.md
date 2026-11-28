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
- LRCLIB exact records and cached records are validated against normalized title, artist credits and duration with a ±3 second tolerance. A missing duration is not accepted as an exact match.
- Manual LRCLIB candidate search uses the candidate ranker, exposes scores and validation reasons, and re-validates a candidate before applying or caching it.

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
| Strict lyrics identity validation, legacy cache rejection and ranked manual candidate selection | Implemented in working branch; Windows CI and device acceptance pending |
| Detected media-session source picker and saved selection | Implemented; awaiting Windows CI and device acceptance |
| Explicit YouTube Music website/source confirmation | Pending |
| LRC editor, validation and export | Implemented in the working branch; CI and device acceptance pending |
| Manual dual-document lyrics with user-labelled, locally persisted per-track translation sidecars | Implemented in working branch; CI and device acceptance pending |
| AI translation provider and in-app word-level translation editor | Pending; requires an opt-in provider integration and timestamp-safe design |
| Metadata-reactive album ambience, fluid gradient and particle field | Implemented in working branch; CI and device performance acceptance pending |
| Real audio-reactive spectrum/waveform input | Pending; requires explicit opt-in Windows audio-session capture implementation |
| Advanced Game Mode | Drag/resize/topmost baseline plus click-through toggle and global restore hotkey implemented; CI and device/game compatibility QA pending |
| Integrations and Customization Studio | Pending |
| Windows device acceptance and release | Pending |

Local translations are attached per track by a user-selected file and an explicitly entered language label. Timed translation lines follow the shared scene position; unsynced translation text is presented as an unsynced lane. The original document remains the source of the playback clock and word karaoke.

The LRC editor exports line-synced and unsynced documents. It deliberately blocks word-synced documents in this editor because the standard LRC format cannot safely preserve per-word timestamps.

Game Mode click-through is optional and reversible with the global shortcut `Ctrl+Alt+Shift+G`; users should keep the main application open to retain a reliable hotkey path while the overlay ignores pointer input.

## Lyrics identity validation

The automatic LRCLIB path validates the provider's exact record before returning it. The service independently validates cache and provider results so an old cache entry or a future provider adapter cannot bypass the same rule.

A result is accepted only when:
- The normalized title matches after removing supported featuring and release-version annotations and normalizing accents and punctuation.
- The expected artist credit matches the returned artist credit. A partial artist match is permitted only when both metadata strings explicitly identify collaboration credits.
- Both expected and returned durations are present and differ by no more than three seconds.

A rejected exact result is not cached or displayed. A previously cached record that fails validation is ignored and deleted when the cache also exposes the library deletion contract.

The manual candidate search uses LRCLIB Search and the shared ranker. Each candidate exposes a score and a reason string. The UI disables selection for candidates that fail the same validator, and the service validates again before a selected candidate is persisted and displayed.

## Release gates

A workstream is complete only after implementation, automated tests, successful Windows CI, matching documentation and the required manual acceptance. A visible control without a working operation is not accepted. A connector must never show Connected without a real handshake or a verified media-session match.

## Verified preview artifact

- Windows CI run: https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/46029075732
- Artifact: `skylyrics-windows-x64-installer`
- Installer size: 78,671,364 bytes
- SHA-256: `7bf079a5b1df1d304947a20cc8dfeb9c25aa62a669d035ffd186a92eaedafc8d`
- The downloaded installer checksum was recomputed locally and matched the checksum file bundled in the artifact.
- This confirms package integrity; installation and interactive behavior still require manual acceptance on Windows 11.
