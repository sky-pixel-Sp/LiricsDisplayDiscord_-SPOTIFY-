# SkyLyrics Desktop 2.0

A native Windows 11 music companion for Spotify Desktop. SkyLyrics reads playback metadata and the system media timeline through Windows GSMTC, retrieves matching lyrics from LRCLIB or user-selected local files, and shares one synchronized scene across the desktop workspace, Focus/Karaoke, Mini Player, Game Mode, Discord Rich Presence and OBS Browser Source.

## Product experience

- **Premium music workspace:** left navigation, central live lyric stage, full-lyrics reader, and a right-side Now Playing/session panel.
- **Focus/Karaoke:** full-screen lyric presentation with previous/current/upcoming context and a clear ESC exit affordance.
- **Mini Player:** compact, resizable floating view of real artwork, track details and active lyric.
- **Game Mode:** movable and resizable lyric overlay with saved placement, opacity, visual presets, optional always-on-top, and Ctrl+Alt+G.
- **Lyrics:** LRCLIB matching, local LRC/Lyricsfile YAML import, SQLite cache, line-synced and word-synced modes only when source timestamps support them.
- **External outputs:** optional Discord Rich Presence over local Discord IPC and OBS Browser Source served on 127.0.0.1 only.
- **Diagnostics:** actual process working set, managed heap, scene revision, artwork payload, playback state and last scene update.
- **Appearance:** consistent Dark and Light tokens, vector iconography, reduced-motion preference, and a calm editorial music aesthetic.

## Platform and architecture

- Windows 11 x64
- C# / .NET 10 / WPF
- Windows Global System Media Transport Controls (GSMTC)
- LRCLIB plus manually imported LRC and Lyricsfile YAML
- SQLite lyrics cache
- Real album artwork from the active Spotify media session
- SVG vector assets rendered with SharpVectors
- Discord Rich Presence over local Discord desktop IPC
- OBS Browser Source served locally on 127.0.0.1 only

GSMTC → PlaybackClock → SyncScheduler → immutable SceneState

The main workspace, Focus/Karaoke, Mini Player and Game Mode consume the same ShellViewModel/SceneState. External outputs use a bounded scene queue so a slow sink cannot block playback synchronization.

## Accuracy, privacy and safety

- Never invent lyric text, translations, artwork, track metadata or timestamps.
- Unsynchronized lyrics are labelled as unsynchronized. Word highlighting only uses source word timestamps.
- Artwork is read from Spotify's GSMTC thumbnail stream, kept memory-only and size-limited. No synthetic album cover is substituted when artwork is unavailable.
- No Spotify private endpoints, DRM bypass, audio loopback capture or audio fingerprinting.
- Discord uses local desktop IPC and a public Application ID; it does not request account tokens or passwords.
- OBS binds only to 127.0.0.1, only accepts GET, limits request headers and displays data via textContent.

## Build from source

Requires the .NET 10 SDK and Windows desktop targeting.

PowerShell:

    dotnet restore .\SkyLyrics.sln
    dotnet build .\SkyLyrics.sln --configuration Release --no-restore
    dotnet test .\tests\SkyLyrics.Tests\SkyLyrics.Tests.csproj --configuration Release
    dotnet run --project .\src\SkyLyrics.Desktop\SkyLyrics.Desktop.csproj

## Windows installer

The repository includes installer/windows/SkyLyrics.iss and Windows CI/release workflows. Windows CI restores and builds Release, runs unit tests, publishes a self-contained win-x64 application, compiles the Inno Setup installer and generates SHA-256 checksums. A release tag such as v2.0.0 triggers the release workflow. The installer is unsigned unless a valid code-signing certificate is configured.

## Discord setup

1. Create a Discord application in the Discord Developer Portal.
2. Copy its public Application ID.
3. Open SkyLyrics Settings, enable Discord Rich Presence and enter the ID.
4. Keep Discord Desktop running.

Rich Presence is disabled by default and no account login token is requested.

## OBS setup

1. Open SkyLyrics Settings and enable OBS Browser Overlay.
2. Save the settings. The listener binds only to 127.0.0.1.
3. In OBS, add a Browser Source with URL http://127.0.0.1:43821/overlay.
4. Use the custom port shown in Settings if the default port was changed.

## Verification boundary

Windows CI is authoritative for compilation, tests, self-contained publishing and installer creation. Successful CI does not replace manual Windows acceptance. Live Spotify playback/reconnect, artwork, hotkey collisions, tray behavior, DPI scaling, Discord IPC and OBS Browser Source must still be exercised on the target Windows desktop. The visual layout should also be reviewed at compact and primary window sizes.
