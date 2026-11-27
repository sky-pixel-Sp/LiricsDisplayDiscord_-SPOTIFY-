# SkyLyrics Desktop

A native Windows 11 desktop companion for Spotify Desktop. It reads playback metadata and timeline through Windows GSMTC, retrieves matching lyrics from LRCLIB or user-selected local files, and publishes immutable synchronized scene state to desktop and optional external displays.

## Platform and stack

- Windows 11 x64
- C# / .NET 10 LTS / WPF
- Windows Global System Media Transport Controls (GSMTC)
- LRCLIB plus manually imported LRC and Lyricsfile YAML
- SQLite lyrics cache
- Real album artwork from the active Spotify media session
- Lucide SVG vectors rendered with SharpVectors
- Discord Rich Presence over local Discord desktop IPC
- OBS Browser Source served locally on 127.0.0.1 only

## Architecture

GSMTC -> PlaybackClock -> SyncScheduler -> immutable SceneState

SceneState -> Main dashboard / Focus view / Floating mini player / Resizable Game Mode
SceneState -> Discord Rich Presence
SceneState -> OBS Browser overlay

Focus and mini-player windows reuse the same ShellViewModel and SyncScheduler. External outputs use a bounded scene queue so a slow sink cannot block playback synchronization.

## Product features

- Spotify Desktop detection, track metadata, playback state and monotonic timeline.
- Exact lyric retrieval with LRCLIB and local LRC/Lyricsfile import.
- SQLite cache, line-synced and word-synced modes where timestamps genuinely exist.
- Premium lyric stage with previous/current/upcoming context and real Spotify artwork.
- Full-screen Focus/Karaoke window, native main-window minimize/maximize, and F11 fullscreen (ESC restores the window).
- Game Mode overlay with drag-to-move, edge/corner resizing, saved position/size, configurable opacity/background/text effect/animation, always-on-top toggle, direct close control, tray action and Ctrl+Alt+G.
- Main-window close exits the app; minimizing uses normal Windows controls. Game Mode is independent of the dashboard visibility and displays the shared synchronized lyric state.
- Notification-area tray menu and global shortcuts: Ctrl+Alt+K, Ctrl+Alt+M, Ctrl+Alt+S and Ctrl+Alt+G.
- Dark/Light themes, reduced motion, synchronization offset and topmost mini-player preference.
- Diagnostics window with live process memory and actual scene/artwork telemetry.
- Optional Discord Rich Presence; user supplies their own public Discord Application ID.
- Optional OBS Browser Source overlay bound to loopback; no LAN listener.

## Accuracy, privacy and safety

- Never invent lyric text, translations, artwork, track metadata or timestamps.
- Unsynchronized lyrics are labelled as unsynchronized. Word highlighting only uses source word timestamps.
- Artwork is read from Spotify's GSMTC thumbnail stream, kept memory-only and size-limited.
- No Spotify private endpoints, DRM bypass, audio loopback capture or audio fingerprinting.
- Discord uses local desktop IPC and a public Application ID; it does not request user account tokens or passwords.
- OBS binds only to 127.0.0.1, only accepts GET, limits request headers and displays data via textContent.

## Build from source

Requires the .NET 10 SDK and Windows desktop targeting.

PowerShell:
    dotnet restore .\SkyLyrics.sln
    dotnet build .\SkyLyrics.sln --configuration Release --no-restore
    dotnet test .\tests\SkyLyrics.Tests\SkyLyrics.Tests.csproj --configuration Release
    dotnet run --project .\src\SkyLyrics.Desktop\SkyLyrics.Desktop.csproj

## Windows installer

The repository includes installer/windows/SkyLyrics.iss and the Windows Release workflow. A tag named v1.0.0 triggers a self-contained Windows x64 publish, tests, Inno Setup compilation and SHA-256 checksum generation. The unsigned installer is published to the GitHub Release. A valid code-signing certificate is required for a signed release.

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

## CI and release boundary

Windows CI restores, builds Release and runs the test suite on windows-latest. The release workflow is separate and runs on a version tag or manual workflow dispatch.

A successful CI build does not replace manual Windows 11 acceptance. Live Spotify reconnect/artwork, hotkey collisions, tray behaviour, DPI scaling, Discord IPC and OBS Browser Source must be exercised on the actual Windows desktop. The idle memory budget also requires measurement rather than assertion.
