# SkyLyrics Desktop

SkyLyrics Desktop is a native Windows 11 x64 companion for Spotify Desktop. It reads track metadata and playback position from Windows Global System Media Transport Controls (GSMTC), then resolves lyrics independently through LRCLIB or user-imported LRC files.

## Current implementation

- Native WPF desktop shell using C# and .NET 10.
- Spotify Desktop session discovery through Windows GSMTC.
- LRCLIB exact metadata lookup using title, artist, album, and duration.
- LRC timestamp parsing, metadata tags, multiple timestamps per line, and offset support.
- Active-line calculation from source-provided timestamps.
- Plain lyrics are explicitly marked UNSYNCED.
- Dark-first interface with a Dark/Light theme switch.
- Manual LRC and TXT import.
- Initial MSTest coverage for parser and line synchronization.
- GitHub Actions Windows x64 build and test workflow.

## Accuracy rules

SkyLyrics never invents lyrics or timestamp values. Word-level highlighting is not enabled yet: the provider response is modeled, but verified word-timestamp payload parsing and rendering remain follow-up work. Plain text is never presented as synchronized. Ambiguous matching, SQLite persistence, smoother animated karaoke transitions, diagnostics, and the signed installer also remain follow-up work.

The application does not scrape Spotify private APIs, bypass DRM, record audio, capture loopback audio, or fingerprint audio. Spotify playback remains controlled by Spotify itself.

## Build locally

Requires the .NET 10 SDK and Windows 10 version 19041 or later for the desktop app.

Commands:

    dotnet restore SkyLyrics.sln
    dotnet test tests/SkyLyrics.Tests/SkyLyrics.Tests.csproj --configuration Release
    dotnet publish src/SkyLyrics.Desktop/SkyLyrics.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true

## Repository status

Phase 1 implementation is in progress on feat/phase-1-native-foundation. A green workflow build and inspected artifact are required before any executable is considered verified. The current workflow publishes a Windows x64 application artifact; it is not yet a signed installer.
