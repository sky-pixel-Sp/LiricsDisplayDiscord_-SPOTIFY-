# SkyLyrics Desktop 2.1 — Preview

SkyLyrics Desktop is a native Windows 11 music companion for synchronized lyrics, cinematic presentation and desktop overlays.

**2.1 status:** active development. This preview is built from the existing 2.0 application foundation. Multi-source playback, the upgraded Lyrics Studio and the Extreme Visual Engine are planned workstreams; they must not be represented as completed integrations until their adapters, UI states and tests are implemented.

## Current foundation

- Windows GSMTC playback metadata and timeline for Spotify Desktop, recognized Windows Media sessions, and browser media sessions.
- LRCLIB lyric lookup, exact-match validation and local SQLite caching.
- User-selected LRC and Lyricsfile YAML import, validated local LRC editor/export, and per-track user-labelled translation sidecars.
- Line synchronization and source-provided word synchronization.
- Shared playback clock and immutable scene state with source line-index identity, seek handling and configurable 60–120 ms lyric transition lead.
- Reduced-motion-aware lyric transitions with QuinticEase, active/previous/upcoming line treatments, and a lower lyrics-panel transition.
- A timeline-driven ambient wave and original floating-note SVGs for instrumental, unsynced or unavailable lyrics. These visuals are deliberately not audio-reactive.
- Optional cinematic startup splash with real host, SQLite, GSMTC and theme startup milestones.
- Bundled Poppins and Bebas Neue font resources with OFL licenses and SHA-verified, pinned Windows build acquisition.
- Artwork-palette radial ambience, low-resolution backdrop, bokeh, grain, vignette, light leak and optional 2.39:1 letterbox.
- Optional ps_3_0 grain and chromatic-aberration shaders, plus adaptive rendering tiers.
- Main workspace, Focus/Karaoke, Mini Player and Game Mode windows.
- Discord Rich Presence through local desktop IPC.
- OBS Browser Source served on loopback only.
- Dark/Light themes, motion preferences and runtime diagnostics.

## SkyLyrics 2.1 upgrade direction

- **Multi-source music:** Spotify Desktop, YouTube Music where an identifiable Windows media session is exposed, and Windows Media / other selected GSMTC sessions.
- **Advanced Lyrics Studio:** original + translation documents, explicit provenance, timestamp calibration, local editor, import/export and library.
- **Cinematic Visual Engine:** artwork-palette ambience, playback-clock waveform motion, SVG note sprites and tiered visual effects. Real audio-reactive visualization remains a separate opt-in future capability.
- **Advanced Game Mode:** click-through, lock/pin, hotkeys, multiple monitors, saved geometry and adaptive rendering.
- **Customization Studio:** versioned themes, typography, layouts, motion presets and safe import/export.

YouTube Music in a browser is exposed, when available, as a Chrome/Edge browser media session. GSMTC metadata does not reliably identify the website, so a source picker or explicit user selection is required where the website identity cannot be verified. The application must label the detected source accurately.

## Platform and architecture

- Windows 11 x64
- C# 14 / .NET 10 / WPF
- Windows Global System Media Transport Controls (GSMTC)
- LRCLIB, local LRC and Lyricsfile YAML
- SQLite lyrics cache
- Discord Rich Presence over local Discord IPC
- OBS Browser Source on 127.0.0.1

The current application keeps one playback clock:

`GSMTC → PlaybackClock → SyncScheduler → immutable SceneState`

The main workspace, Focus/Karaoke, Mini Player, Game Mode and external outputs consume the same synchronized scene. Network and output work must not block the synchronization scheduler.

## Accuracy, privacy and safety

- Never invent lyric text, translations, artwork, track metadata or timestamps.
- Unsynchronized lyrics remain visibly unsynchronized. Word highlighting requires source word timestamps.
- Automatic LRCLIB results require normalized title and artist agreement plus a source duration within ±3 seconds. Missing duration is not treated as a verified match.
- Title normalization removes featuring annotations, remaster-year suffixes, (Versions) and (Live) tags while preserving the independent duration check. Accent and punctuation differences are normalized.
- Previously cached records that fail the current signature check are ignored and deleted when the cache exposes library deletion.
- When automatic lookup finds no valid record, the user can search LRCLIB candidates. Candidate cards show a 0–100 score and validation reasons; only candidates passing the same title, artist and duration validator can be selected.
- No Spotify private endpoints, DRM circumvention, credential scraping or hidden browser automation.
- Audio-reactive modes require an explicitly enabled and visible audio-session input. GSMTC metadata alone is not audio data.
- Discord uses local desktop IPC and a public Application ID; no bot or user token is requested.
- OBS binds only to 127.0.0.1 by default.

## Build from source

Requires the .NET 10 SDK and Windows desktop targeting. The Windows build script downloads the pinned Poppins and Bebas Neue TrueType files from Google Fonts at a fixed commit, verifies their Git blob hashes, file sizes and font-family names, and compiles the ps_3_0 shaders with the Windows SDK fxc.exe. The two SIL Open Font License files are included in the repository. The first build therefore requires network access to raw.githubusercontent.com and a Windows SDK installation that includes fxc.exe.

PowerShell:

    dotnet restore .\SkyLyrics.sln
    dotnet build .\SkyLyrics.sln --configuration Release --no-restore
    dotnet test .\tests\SkyLyrics.Tests\SkyLyrics.Tests.csproj --configuration Release
    dotnet run --project .\src\SkyLyrics.Desktop\SkyLyrics.Desktop.csproj

## Windows installer and preview status

Windows CI builds Release, runs unit tests, publishes a self-contained win-x64 application, compiles the Inno Setup installer and generates a SHA-256 checksum. Preview packages are not production releases and may change during the 2.1 upgrade. The installer is unsigned unless a valid code-signing certificate is configured.

A successful CI run proves the tested build and packaging steps completed. It does not replace manual acceptance on a Windows 11 desktop with real Spotify/YouTube Music/Windows Media sessions, Discord IPC, multiple monitors and display scaling.
