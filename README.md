# SkyLyrics Desktop

SkyLyrics Desktop is a native Windows x64 companion for Spotify Desktop. It reads track metadata and playback position through Windows Global System Media Transport Controls (GSMTC), then resolves lyrics through LRCLIB or user-imported lyric files.

## Implemented

- Native C# / .NET 10 / WPF desktop application.
- Spotify Desktop session discovery and playback metadata through Windows GSMTC.
- LRCLIB exact metadata lookup using title, artist, album, and duration.
- Conservative response validation: title and artist must match; album must match when both sources provide it; duration tolerance is 2 seconds.
- LRC parser with line timestamps, metadata tags, multiple timestamps per line, and `[offset:]`.
- LRCLIB Lyricsfile YAML 1.0 parsing for actual `lines[].words` word timestamps.
- Active-line and active-word synchronization from provider timestamps only.
- Adaptive UI refresh: 110 ms with playing word-sync lyrics, 260 ms with playing line-sync lyrics, and 900 ms while paused/unsynced/no track.
- Animated active-line transitions and automatic scroll following without rebuilding the lyric list on every polling tick.
- Local import for `.lrc`, Lyricsfile YAML, and plain `.txt`.
- Dark/Light theme switch.
- Discord Rich Presence integration prototype (Phase 5); runtime compatibility with the current Discord desktop RPC authentication requirements is not yet verified.
- Discord Application ID setup UI and local storage are implemented. The current raw IPC prototype is not release-ready; the supported no-OAuth implementation needs the official Discord Social SDK.
- SQLite offline cache stored in `%LOCALAPPDATA%\SkyLyrics\lyrics.db`, with 30-day expiry and a track identity based on normalized artist/title/album and rounded duration.
- Seven-plus additional cache and matching tests; 17 automated tests pass in the verified CI build.
- Per-user Windows x64 installer built with Inno Setup, with Start Menu shortcut, optional desktop shortcut, and uninstaller.
- GitHub Actions builds/tests the app, packages the installer, and smoke-tests install/uninstall.

## Discord integration status

Phase 5 currently contains a Rich Presence UI and a raw IPC prototype, but it is **not release-ready**. Discord's general RPC commands require an authenticated RPC client; a raw unauthenticated `SET_ACTIVITY` call is not a validated no-login integration. The supported desktop Rich Presence path without OAuth uses Discord's official Social SDK. That native SDK must be obtained from the authenticated [Discord Developer Portal](https://discord.com/developers/applications) and integrated under its SDK terms.

The current CI run verifies compilation, 25 automated tests, Windows x64 publishing, and installer install/uninstall smoke tests. It does **not** verify a live Discord connection. See [docs/DISCORD_RICH_PRESENCE.md](docs/DISCORD_RICH_PRESENCE.md) for the blocker and required SDK path.

## Accuracy rules

SkyLyrics never invents lyrics or timestamps. Word highlighting is available only when LRCLIB's Lyricsfile contains valid source word timestamps. Invalid word timing is rejected; where verified LRC timestamps are also available, the app falls back to line sync. Plain lyrics are always marked **UNSYNCED**.

LRCLIB responses that do not match the active Spotify track metadata are rejected rather than displayed. The current implementation uses the exact `/api/get` endpoint; it does not silently choose an ambiguous search result.

The app does not scrape Spotify private APIs, bypass DRM, record audio, capture loopback audio, or fingerprint audio. Spotify remains the playback controller.

## Distribution and verification

The verified GitHub Actions run is [#45807615647](https://github.com/sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-/actions/runs/45807615647) on commit `90b1fbc2afe1931a0101bd5dcb7ff748b9c6d643`.

- Automated tests: **17 passed, 0 failed**.
- Self-contained Windows x64 application publish: passed.
- Installer compilation: passed.
- Installer installation and uninstallation smoke test: passed.
- Setup artifact: `SkyLyrics-Desktop-Setup-x64.exe`, 49,365,701 bytes.
- Application payload: `SkyLyrics.Desktop.exe`, 167,852,487 bytes.

The installer is currently **unsigned**. Windows SmartScreen may show an unknown-publisher warning. The app has been built and install/uninstall-tested by GitHub Actions, but has not yet been manually launched on the user's own laptop.

## Remaining work

- LRCLIB candidate search and a manual ambiguous-match selector.
- Floating mini-player and dedicated diagnostics screen.
- Manual play/pause, seeking, track-switch and reconnect validation with Spotify Desktop on the target laptop.
- Figma editable screen frames; the Figma file exists but editable frames were not completed because the Figma tool call limit was reached.
- Code signing and release hardening.

## Build locally

Requires the .NET 10 SDK and Windows 10 version 19041 or later for the desktop app.

    dotnet restore SkyLyrics.sln
    dotnet test tests/SkyLyrics.Tests/SkyLyrics.Tests.csproj --configuration Release
    dotnet publish src/SkyLyrics.Desktop/SkyLyrics.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true
