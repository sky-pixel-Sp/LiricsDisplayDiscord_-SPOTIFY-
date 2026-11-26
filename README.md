# SkyLyrics Desktop

A native Windows desktop companion that detects the active Spotify Desktop playback session and displays correctly matched, timestamped lyrics with animated line-level and word-level highlighting.

## Locked product decisions

- Platform: Windows 11 x64
- Application: C# / .NET 10 LTS / WPF
- Spotify integration: Windows Global System Media Transport Controls (GSMTC), targeting Spotify Desktop
- Lyrics: LRCLIB API plus user-owned local LRC files and a local SQLite cache
- Synchronization: line-level and word-level when the provider supplies word timestamps
- Themes: Dark and Light
- Distribution: Windows x64 installer
- Primary repository branch: `main`

## Repository status

This repository was empty when the project was initialized. The first commits establish the product specification and engineering plan. No executable or application source code is claimed to exist yet.

## Documentation

- [Product specification](docs/PROJECT_SPECIFICATION.md)
- [Technical implementation plan](docs/TECHNICAL_IMPLEMENTATION_PLAN.md)
- [UI and Figma specification](docs/UI_FIGMA_SPECIFICATION.md)

## Important accuracy and platform constraints

Spotify playback metadata is not the same thing as licensed lyrics. SkyLyrics obtains playback metadata from the Windows media session and retrieves lyrics independently from supported lyric sources. It must never fabricate lyrics or word timestamps. If only plain lyrics are available, the UI must clearly show that they are unsynchronized.

The initial release targets Spotify Desktop on Windows. Spotify Web Player support is not part of this locked scope.

## Build status

Planning/documentation only. The Windows application, tests, installer, and CI build pipeline have not yet been implemented. A release is complete only after a real Windows build, automated tests, installer validation, and artifact inspection succeed.
