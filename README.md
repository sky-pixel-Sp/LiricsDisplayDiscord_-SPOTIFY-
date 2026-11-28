# SkyLyrics Desktop

A native Windows desktop companion that detects Spotify Desktop playback and displays correctly matched, timestamped lyrics with cinematic presentation. Its core emits immutable scene snapshots to independent output sinks.

## Locked product decisions

- Platform: Windows 11 x64
- Language/runtime: C# / .NET 10 LTS
- Desktop UI: WPF with MVVM and dependency injection
- Playback source: Windows Global System Media Transport Controls (GSMTC)
- Lyrics: LRCLIB plus user-selected local LRC files
- Persistence: SQLite
- Outputs: WPF cinema/overlay, local OBS overlay, official Discord Rich Presence, and optional Discord Embedded Activity
- Repository: sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-

## Architecture

GSMTC -> PlaybackClock -> SyncScheduler -> immutable SceneState

SceneState -> WPF output
SceneState -> OBS output
SceneState -> Discord Rich Presence
SceneState -> Discord Embedded Activity

The core does not depend on any presentation or external-output implementation. Each sink can be independently enabled or disabled.

## Implementation status

- The default branch currently contains the product and engineering specifications.
- The Phase 0 branch adds the .NET solution, immutable scene contract, output-router boundary, WPF composition root, unit tests, and Windows CI.
- Spotify integration, lyrics retrieval, synchronization, rendering, OBS, and Discord are not part of Phase 0.

See docs/PHASE_0_BASELINE.md, docs/PROJECT_SPECIFICATION.md, docs/TECHNICAL_IMPLEMENTATION_PLAN.md, and docs/UI_FIGMA_SPECIFICATION.md.

## Accuracy and privacy requirements

- Never invent lyrics, timestamps, word timings, artwork, or track metadata.
- Plain lyrics must be labelled Unsynced.
- Never synthesize word timings by splitting a line evenly.
- Use Windows GSMTC for playback metadata and position.
- Do not use private Spotify endpoints, capture audio, or bypass DRM.
- Discord integration must use official Rich Presence / Embedded App SDK paths only.
- OBS serving must bind to loopback (127.0.0.1) only.

## Build

Requires the .NET 10 SDK and Windows desktop targeting support.

PowerShell commands:
    dotnet restore .\SkyLyrics.sln
    dotnet build .\SkyLyrics.sln --configuration Release --no-restore
    dotnet test .\tests\SkyLyrics.Tests\SkyLyrics.Tests.csproj --configuration Release
    dotnet run --project .\src\SkyLyrics.Desktop\SkyLyrics.Desktop.csproj

The desktop project targets Windows x64. The Linux/macOS command-line build path is not a supported runtime target for this application.
