# SkyLyrics Desktop

SkyLyrics Desktop is a native Windows 11 x64 companion for Spotify Desktop. It reads track metadata and playback position through Windows Global System Media Transport Controls (GSMTC), then resolves lyrics through LRCLIB or user-imported lyric files.

## Implemented

- Native C# / .NET 10 / WPF desktop application.
- Spotify Desktop session discovery and playback metadata through Windows GSMTC.
- LRCLIB exact metadata lookup using title, artist, album, and duration.
- LRC parser with line timestamps, metadata tags, multiple timestamps per line, and `[offset:]`.
- LRCLIB Lyricsfile YAML 1.0 parsing for real `lines[].words` word timestamps.
- Active-line and active-word calculation using provider-supplied timestamps only.
- Animated active-line transitions and automatic scroll following without rebuilding the lyric list on every playback poll.
- Manual import for `.lrc`, Lyricsfile YAML, and plain `.txt`.
- Dark/Light theme switch.
- Local plain text is explicitly marked **UNSYNCED**.
- Invalid word timing is rejected; the app can fall back to provider-supplied LRC line timing when available.
- Seven automated tests covering LRC parsing, offsets, active-line matching, word-sync parsing, active-word matching, unsynced fallback, and invalid word timestamps.
- GitHub Actions Windows x64 workflow that restores, builds, tests, publishes a self-contained app, verifies the executable, and uploads artifacts.

## Accuracy rules

SkyLyrics never invents lyrics or timestamps. Word highlighting is available only when LRCLIB's Lyricsfile contains valid source word timestamps. A line with malformed word segments is shown as ordinary text rather than being assigned guessed boundaries. Plain lyrics are never presented as synchronized.

The app does not scrape Spotify private APIs, bypass DRM, record audio, capture loopback audio, or fingerprint audio. Spotify remains the playback controller.

## Current limitations / next phases

- SQLite local lyrics cache is not implemented yet.
- LRCLIB search fallback and a user-facing ambiguous-match selector are not implemented; the current provider request uses the exact `/api/get` endpoint.
- Playback/session reconnection and seek behavior are implemented through polling, but still need manual testing with Spotify Desktop on a Windows machine.
- A signed Windows installer is not implemented. CI currently publishes a self-contained Windows x64 executable artifact.
- This executable has been built and tested by GitHub Actions; it has not yet been manually launched on the user's laptop.
- Figma file exists, but editable screen frames have not yet been completed.

## Build locally

Requires the .NET 10 SDK and Windows 10 version 19041 or later for the desktop app.

    dotnet restore SkyLyrics.sln
    dotnet test tests/SkyLyrics.Tests/SkyLyrics.Tests.csproj --configuration Release
    dotnet publish src/SkyLyrics.Desktop/SkyLyrics.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true

## CI verification

The Windows x64 workflow publishes the `SkyLyrics-Desktop-win-x64` artifact and `SkyLyrics-test-results`. The artifact is self-contained and does not require the user to install the .NET runtime separately.
