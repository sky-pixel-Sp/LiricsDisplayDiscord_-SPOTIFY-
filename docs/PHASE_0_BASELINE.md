# Phase 0 — Solution Skeleton and Windows CI

## Scope

Phase 0 establishes the buildable application foundation only. It does not implement Spotify session discovery, playback timing, LRCLIB requests, lyric parsing, persistence, visual effects, OBS, or Discord.

## Project boundaries

- **SkyLyrics.Core** contains platform-neutral scene contracts. SceneState and its nested scene records are immutable snapshots; word timing is represented only when the source provides it.
- **SkyLyrics.Application** owns output contracts and routes scene snapshots to enabled sinks. A failing sink is logged and isolated from the other sinks.
- **SkyLyrics.Desktop** is the WPF composition root. The Generic Host provides dependency injection and logging.
- **SkyLyrics.Tests** validates the scene contract and output-routing behavior.

Windows and provider-specific code will be introduced behind application/core contracts in later phases. The WPF project is explicitly Windows x64; Core and Application remain platform-neutral.

## Windows CI

The workflow restores and builds the full solution using the pinned .NET 10 SDK, runs the unit tests, and uploads TRX results. It is triggered by pushes to main and feat branches, pull requests targeting main, and manual dispatch.

CI does not launch the WPF window because that requires an interactive desktop session. It does compile the complete Windows desktop target.

## Exit criteria

- [x] .NET 10 solution and project references are defined.
- [x] WPF desktop composition root uses dependency injection and logging.
- [x] Immutable scene-state and output-sink contracts exist.
- [x] Output delivery isolates individual sink failures.
- [x] Unit tests cover core invariants and output routing.
- [ ] Windows CI run is green on the Phase 0 branch.

## Next phase

Phase 1 adds the Windows GSMTC session adapter and monotonic PlaybackClock. It will not add lyrics retrieval or rendering.
