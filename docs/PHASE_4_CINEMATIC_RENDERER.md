# Phase 4 — Cinematic Renderer and Real Artwork

## Scope

Creates the WPF presentation layer for synchronized lyrics while keeping every timing decision in the Phase 3 SyncScheduler. The UI animation layer never feeds timestamps back to the playback clock.

## Real artwork source

- Album art is read from the active Spotify Desktop GSMTC media-properties Thumbnail stream.
- Artwork is copied to an immutable byte array and carried through TrackMetadata and SceneState; no artwork is requested from an invented URL, fetched from a third-party search, or synthesized.
- Thumbnail payloads larger than 8 MiB are skipped before allocating their buffer.
- WPF decodes artwork with a 512-pixel decode width and caches it in memory. The image is not persisted to disk or sent to a remote service.
- If Spotify does not expose a thumbnail or its stream cannot be read/decoded, the application shows a truthful unavailable state and does not draw a generic album-cover substitute.
- Track identity comparison excludes artwork bytes so changes in image buffers cannot be mistaken for track changes.

## Real vector assets

- The application includes original Lucide SVG geometry from the official Lucide repository: headphones, list-music, audio-lines, settings-2 and maximize-2.
- SVGs are rendered as vector artwork through SharpVectors.Wpf 1.8.6, which supports the .NET 10 WPF target.
- The Lucide ISC license is included in THIRD_PARTY_NOTICES.md. SharpVectors.Wpf remains a NuGet dependency under its BSD-3-Clause license.
- Product typography uses the installed Windows Segoe UI Variable font family; no font files are bundled or redistributed.

## Cinematic presentation

- Premium dark editorial layout: track identity card, low-opacity artwork ambience, large current line, previous and upcoming lines, active source-timed word, and a full-lyrics transcript panel.
- Current line uses short opacity and vertical-ease transitions.
- Artwork crossfades only when actual artwork is available.
- System client-area animation preferences are honored. Motion changes only visual properties and does not alter source timestamps.
- No audio capture, audio recognition, third-party artwork scraping, generated cover art, or hardcoded sample lyrics are introduced.
- The full-lyrics panel remains available for Unsynced content, while active playback highlighting stays unavailable for unsynchronized sources.

## Validation

- Core tests verify immutable artwork pass-through to SceneState and previous/current/next line context.
- Windows CI builds the .NET 10 WPF project with SharpVectors SVG resources and runs all unit tests.
- Live Spotify thumbnail retrieval, high-DPI rendering, and visual animation review still require a real Windows 11 machine with Spotify Desktop.

## Exit criteria

- [x] GSMTC thumbnail reader with an 8 MiB payload cap.
- [x] Artwork flows from playback metadata into SceneState.
- [x] WPF decodes real artwork with a bounded decode width.
- [x] Explicit no-artwork state; no generic or synthetic cover.
- [x] Actual Lucide SVG files included with the license.
- [x] SharpVectors integration registered for WPF.
- [x] Animated active line and artwork fade implemented as presentation-only effects.
- [x] Previous/current/upcoming lyric line context rendered.
- [ ] Live Spotify artwork and visual acceptance on Windows 11.

## Next phase

Phase 5 builds the focus/karaoke view, floating mini player, tray integration, keyboard shortcuts and window modes using the same immutable SceneState and artwork pipeline.
