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
- Lyric scene lines now preserve source LineIndex and track identity; active-line identity changes independently of line text, including repeated lyric text and same-index lines on different tracks. The scheduler supports a configurable 60–120 ms lead transition and suppresses early transitions while paused.
- MainWindow lyric transitions are driven by ActiveLyricLineIndex only. Active, previous, upcoming and lower lyrics panel transitions use QuinticEase and honor the Windows animation preference and Reduce Motion.
- The no-lyrics/instrumental stage has a timeline-driven waveform and original SVG music-note sprites; no audio capture or synthetic lyrics/timestamps are used.
- A skippable splash screen reports actual host, preferences/SQLite cache, GSMTC and theme startup stages, with a 2.2 second auto-close followed by a short dissolve.
- Poppins and Bebas Neue are downloaded at a pinned Google Fonts commit during Windows build. The build validates Git blob SHA-1, exact file size, TTF header and family name table. OFL licenses are committed.
- Cinematic presentation includes a 96 pixel artwork backdrop, artwork-palette gradients, bokeh, grain, vignette, light leak, optional 2.39:1 letterbox, and low-intensity ps_3_0 shader effects.
- Adaptive visual quality observes frame-time averages over two seconds and steps down when the average exceeds 20 ms.
- LRCLIB exact records and cached records are validated against normalized title, artist credits and duration with a ±3 second tolerance. A missing duration is not accepted as an exact match.
- Manual LRCLIB candidate search uses the candidate ranker, exposes scores and validation reasons, and re-validates a candidate before applying or caching it.

## Remaining work and manual acceptance

- Real audio-reactive spectrum or waveform capture is not implemented. The current waveform is deliberately driven by the playback timeline, not captured audio.
- The generated vector logo and vector splash background are used instead of a PNG logo or raster splash image. A new ICO was not substituted; the existing app icon is retained.
- Font files are acquired and verified during Windows build rather than committed as binary files. The fonts are embedded in the resulting application resources.
- HLSL effects are compiled on Windows with the SDK fxc.exe; a Linux/non-Windows build path for those shaders is not provided.

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
| Line-index lyric identity, early transition scheduling and reduced-motion-aware animation | Implemented; Windows CI and seek/pause/repeat-line device acceptance pending |
| Timeline-driven no-lyrics waveform and original SVG music-note visuals | Implemented; Windows CI and GPU/performance acceptance pending |
| Startup splash with real startup milestones and skip/timeout | Implemented; Windows CI and device acceptance pending |
| Poppins/Bebas Neue resource acquisition, font metadata validation and OFL license inclusion | Implemented in Windows build pipeline; CI and runtime font fallback acceptance pending |
| Cinematic textures, 96-pixel artwork backdrop, ps_3_0 shader effects and adaptive quality | Implemented in working branch; Windows SDK shader build and GPU acceptance pending |
| Optional 2.39:1 letterbox and unified main-workspace visual controls | Implemented; Windows CI and device acceptance pending |
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

## Visual engine implementation details

The no-lyrics wave and note field are generated from playback position plus elapsed playback time, and are not presented as an audio spectrum. The visual renderer stops when paused, hidden, reduced-motion is enabled, or the effective quality tier reaches Reduced.

The splash percentage changes only at registered startup service boundaries. The splash is skipped when disabled in Settings or when the user presses a key or clicks it. The automatic timeout is 2.2 seconds with a short close dissolve.

The 96-pixel artwork bitmap is decoded independently from the 512-pixel artwork used by the main artwork display. Cinematic textures and the shader effects are confined to the stage layers; they are not applied as large-window blur or shadow effects.

The build script downloads the two Google Fonts files from a pinned commit, verifies each Git blob SHA-1 and family-name table, and places them under the WPF resource paths in the intermediate build directory. The matching OFL text files remain in source control.

The optional letterbox is a pure geometry calculation for a 2.39:1 content aspect ratio. It does not alter source lyric timestamps or the scheduler's playback clock.

## Release gates

A workstream is complete only after implementation, automated tests, successful Windows CI, matching documentation and the required manual acceptance. A visible control without a working operation is not accepted. A connector must never show Connected without a real handshake or a verified media-session match.

## Verified preview artifact

- Windows CI run: https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/46029075732
- Artifact: `skylyrics-windows-x64-installer`
- Installer size: 78,671,364 bytes
- SHA-256: `7bf079a5b1df1d304947a20cc8dfeb9c25aa62a669d035ffd186a92eaedafc8d`
- The downloaded installer checksum was recomputed locally and matched the checksum file bundled in the artifact.
- This confirms package integrity; installation and interactive behavior still require manual acceptance on Windows 11.
