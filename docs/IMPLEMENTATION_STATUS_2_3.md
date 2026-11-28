# SkyLyrics Desktop 2.3 — Real Asset Edition

**Target:** Windows 11 x64 · C# 14 · .NET 10 · WPF  
**Working branch:** `feature/skylyrics-2.3-real-assets`  
**Baseline branch:** `feature/skylyrics-2.2-perfecting`  
**Audited baseline commit:** `f09976d100827e2788d712d329fae41ea39277b1`

## Package status

| Package | State | Evidence / remaining work |
|---|---|---|
| P0 — Audit and inventory | <span>Completed as repository inspection</span> | Inventory and license register added. No Windows build was run in this environment. |
| P1 — Original raster backgrounds | Not complete | No master raster background is committed yet. |
| P2 — Real animation assets | Not started | No GIF/video/sprite media committed. |
| P3 — Visualizer and texture collection | Not started | Existing visualizer is rendered by application code; it is not an audio-sample stream. |
| P4 — Asset catalog and browser | Not started | Implement discovery, real-file previews, filtering, favorites, and generated metadata. |
| P5 — Native WPF integration | Not started | Add asset selection to the existing main workspace without replacing playback/lyrics systems. |
| P6 — Performance | Not started | Add bounded decoding, caching, visibility-aware playback, quality profiles, and reduced-motion behavior. |
| P7 — Automated tests and installer | Not started | Run Windows CI and inspect packaged outputs and manifest. |
| P8 — Final acceptance and handoff | Not started | Manual Windows 11 acceptance remains required. |

## Verified baseline observations

- The 2.3 branch already existed and had the same head as the supplied 2.2 branch when audited.
- The desktop project targets `net10.0-windows10.0.19041.0`, enables WPF, and targets x64.
- The current desktop project version metadata is 2.2.0-preview.1.
- Existing SVG backgrounds and textures are not a substitute for the requested raster/animated library.
- Existing lyrics timing is implemented separately in `SkyLyrics.Application/Synchronization/SyncScheduler.cs`; decorative media must not be wired into that scheduler.
- Existing CI builds, tests, publishes self-contained win-x64 output, compiles an Inno Setup installer, and writes build-size/checksum metadata.

## Build and test evidence

No build or test run has been performed for 2.3 yet. The current execution environment is not a Windows build runner and does not expose the .NET SDK. A successful previous CI run, if present in repository history, is not evidence for 2.3 changes.

## Size accounting

| Measurement | Current 2.3 value |
|---|---:|
| Source masters | 0 committed bytes |
| Optimized media variants | 0 committed bytes |
| Published application | Not built |
| Installer | Not built |
| Estimated installed size | Not measured |

No padding, duplicate media, or artificial binary inflation is permitted.
