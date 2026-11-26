# SkyLyrics Desktop

SkyLyrics Desktop is a native Windows x64 companion for Spotify Desktop. It reads track metadata and playback position through Windows Global System Media Transport Controls (GSMTC), then resolves lyrics through LRCLIB or user-imported lyric files.

## Implemented

- Native C# / .NET 10 / WPF desktop application.
- Spotify Desktop session discovery and playback metadata through Windows GSMTC.
- LRCLIB exact metadata lookup using title, artist, album, and duration.
- Conservative response validation: title and artist must match; album must match when both sources provide it; duration tolerance is 2 seconds.
- LRC parser with line timestamps, metadata tags, multiple timestamps per line, and [offset:].
- LRCLIB Lyricsfile YAML 1.0 parsing for actual lines[].words word timestamps.
- Active-line and active-word synchronization from provider timestamps only.
- Local import for .lrc, Lyricsfile YAML, and plain .txt.
- SQLite offline cache stored in %LOCALAPPDATA%\SkyLyrics\lyrics.db, with 30-day expiry and a track identity based on normalized artist/title/album and rounded duration.
- Dark/Light theme switch.
- Automatic lyric following uses content-relative coordinates, avoids cumulative vertical-offset errors, and pauses for five seconds after manual mouse-wheel or keyboard scrolling.
- Appearance Studio with three independent catalogs: **30 animations**, **30 text effects**, and **30 gradient backgrounds**. The chosen IDs are persisted locally in %LOCALAPPDATA%\SkyLyrics\appearance.json.
- Game Mode hides the main window and displays only the active timestamped lyric line in a borderless, topmost overlay at the top of the screen. Right-click the overlay to exit. It runs in the same process, so ending SkyLyrics also closes the overlay.
- Discord Rich Presence publishes the current Spotify track and the currently active timestamped lyric line in the activity state. The line is updated when the active lyric changes. Unsynchronized text is never advertised as a current lyric.
- Discord Application ID setup UI and local storage. No bot token, password, or user OAuth login is required for desktop Rich Presence through RPC. See docs/DISCORD_RICH_PRESENCE.md.
- Per-user Windows x64 installer built with Inno Setup, with Start Menu shortcut, optional desktop shortcut, and uninstaller.
- GitHub Actions builds/tests the app, packages the installer, and smoke-tests install/uninstall.

## Discord Rich Presence

The activity details show the current Spotify track. The activity state shows the active synchronized lyric line, with artist metadata where the 128-character Discord field allows it. A paused track does not display a continuing playback timer.

The feature requires Discord Desktop to be running, a registered public Application ID, and Discord activity sharing to be enabled. The direct desktop Rich Presence RPC path is documented by Discord as usable without calling the Social SDK authentication flow. See the official [Rich Presence without authentication guide](https://github.com/discord/discord-api-docs/blob/main/developers/discord-social-sdk/development-guides/setting-rich-presence.mdx) and [RPC command reference](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx).

Only timestamped source lyrics are sent as the current line. Plain TXT/unsynced content is not treated as synchronized and is not sent as a live lyric.

## Accuracy rules

SkyLyrics never invents lyrics or timestamps. Word highlighting is available only when LRCLIB's Lyricsfile contains valid source word timestamps. Invalid word timing is rejected; where verified LRC timestamps are also available, the app falls back to line sync. Plain lyrics are always marked **UNSYNCED**.

LRCLIB responses that do not match the active Spotify track metadata are rejected rather than displayed. The current implementation uses the exact /api/get endpoint; it does not silently choose an ambiguous search result.

The app does not scrape Spotify private APIs, bypass DRM, record audio, capture loopback audio, or fingerprint audio. Spotify remains the playback controller.

## v0.4.0 development verification

The v0.4.0 implementation is being built on feat/v0.4.0-stability-customization-game-mode. Do not treat the changes as a released installer until the branch's Windows x64 CI build, automated tests, publish, and installer smoke tests complete successfully.

## Build locally

Requires the .NET 10 SDK and Windows 10 version 19041 or later for the desktop app.

    dotnet restore SkyLyrics.sln
    dotnet test tests/SkyLyrics.Tests/SkyLyrics.Tests.csproj --configuration Release
    dotnet publish src/SkyLyrics.Desktop/SkyLyrics.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true
