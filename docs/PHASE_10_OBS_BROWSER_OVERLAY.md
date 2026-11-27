# Phase 10 — OBS Browser Overlay

## Delivered
- OBS Browser Source overlay is served by the desktop process on a configurable TCP port bound exclusively to 127.0.0.1.
- Routes: /overlay (transparent HTML), /state (current SceneState projection JSON) and /health.
- Overlay includes no external fonts, libraries, image URLs or tracking resources.
- It uses textContent for lyric and metadata rendering, not HTML injection.
- Scene projection carries only active metadata and previous/current/upcoming lyric text from SceneState.
- OBS output is opt-in and disabled by default. The port defaults to 43821 and is configurable in Settings.
- Listener shuts down with the app and rebinds when preferences change.

## OBS setup
1. Start SkyLyrics Desktop and open Settings.
2. Enable OBS Browser Overlay and save.
3. In OBS, add a Browser Source and set its URL to http://127.0.0.1:43821/overlay. Replace the port if customized.
4. Set Browser Source dimensions to the desired canvas region. The page background is transparent.

## Security boundary
- Listener binds only to loopback; it is not exposed to the LAN or public internet.
- Only GET requests are accepted. Request headers are limited to 8 KiB and read timeouts are bounded.
