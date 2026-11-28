# SkyLyrics Desktop 2.3 — Real Asset Edition

**Target:** Windows 11 x64 · C# 14 · .NET 10 · WPF  
**Working branch:** `feature/skylyrics-2.3-real-assets`  
**Base branch:** `feature/skylyrics-2.2-perfecting`  
**Last code commit verified by Windows CI:** `48de3a08fd59080f585a8fdcb8ad57aff3cf4bdc`  
**Windows CI:** run #263 · 2026-11-28 · success

## Package status

| Package | State | Evidence / remaining work |
|---|---|---|
| P0 — Audit and inventory | **Completed** | Existing repository visual inventory and provenance gaps documented. |
| P1 — Original raster backgrounds | **Partial** | One original Aurora Fjord starter package was produced in the working artifact area. It is **not committed to the repository or included in the installer**. Seven other background themes and committed master/display/thumbnail variants remain. |
| P2 — Real animation assets | **Partial** | WPF preview supports GIF frame playback and MP4/WebM preview with hide/deactivate pause and Reduced Motion guard. No GIF/video/sprite media files are committed, so real animation playback has not been accepted. |
| P3 — Visualizer and texture collection | Not complete | No visualizer scene pack, light-leak/particle/grain collection, or artwork files are committed. No GSMTC audio samples are fabricated or claimed. |
| P4 — Asset catalog and browser | **Partial** | Disk discovery, stable IDs, image/GIF header metadata, file size, SHA-256, source/license manifest, category/search/favorites, real-file preview, apply/preview/reset, and missing/corrupt handling are implemented. `WriteCatalogAsync` can export a catalog; the repository catalog remains empty because no media files are committed. |
| P5 — Native WPF integration | **Partial** | Main navigation opens the separate Asset Library and the main workspace can load a selected raster background from disk. Focus/Karaoke, Mini Player, Game Mode and splash backgrounds are not yet connected. Animation and visualizer preferences are stored but not fully applied to every view. |
| P6 — Performance and resource management | **Partial** | Preview decodes on demand at bounded widths; GIF/video preview pauses when hidden or inactive and respects Reduced Motion. Full cache policy, safe cleanup, per-window visibility lifecycle, and memory profiling remain. |
| P7 — Automated tests and installer | **CI passed** | Windows runner #263: Release build success, 187/187 unit tests passed, self-contained win-x64 publish success, Inno Setup success, installer checksum verified, installer and test results uploaded. |
| P8 — Final acceptance and handoff | Not complete | Physical Windows 11 device acceptance, visual inspection, audio-reactive validation, memory checks and full media library validation remain. |

## Implemented code

- Added `AssetLibraryPreferences` with selected background/animation/visualizer IDs, favorites and Low/Balanced/High quality profile.
- Added `AssetCatalogService` for actual file enumeration, deterministic IDs, file sizes, SHA-256, PNG/JPEG/WebP/GIF metadata, GIF frame count/duration, source/license manifest ingestion, and catalog export.
- Added native `AssetLibraryWindow` with category selection, search, favorites, real-file preview, media details, apply, preview, reset, quality selection and safe error reporting.
- Added a separate Asset Library navigation item; the existing lyrics Library remains separate.
- Added main workspace background loading from `Assets/Library/Backgrounds/{asset-id}.{webp|png|jpg|jpeg}`.
- Updated project/package metadata and Windows workflow/installer version to 2.3.0-preview.1.

## Windows CI evidence

**Run:** #263  
**Verified implementation commit:** `48de3a08fd59080f585a8fdcb8ad57aff3cf4bdc`  
**Installer:** `SkyLyricsDesktop-Setup-win-x64-2.3.0.263.exe`

| Check | Result |
|---|---:|
| Restore | Success |
| Release build | Success |
| Unit tests | 187 passed · 0 failed · 0 errors · 0 timeouts |
| Self-contained win-x64 publish | Success |
| Inno Setup installer compile | Success |
| Installer verification and SHA-256 | Success |
| Installer artifact upload | Success |
| Test-results artifact upload | Success |

The TRX contains the 187 executed test outcomes. The new asset-service tests cover stable IDs, actual byte size/hash, dimensions read from a file header, malformed media handling, thumbnail exclusion, missing-root behavior, and preference normalization. The tests do not constitute a real visual/media acceptance test.

**Installer SHA-256**

`e37f7bdd020ab46a3772c2a568416adb8418cf501e894084d58e0b1818779e49`

## Size accounting

All repository media folders are currently empty of qualifying raster/animated files. The locally generated Aurora package is separate from the repository and installer.

| Measurement | Exact bytes | Decimal size | Notes |
|---|---:|---:|---|
| Committed source masters | 0 | 0 MB | No source media committed |
| Committed optimized media variants | 0 | 0 MB | No display/thumb media committed |
| Local Aurora master PNG (outside repo) | 2,249,034 | 2.249 MB | 1672×941, generated source; not in installer |
| Local Aurora display WebP (outside repo) | 86,286 | 0.086 MB | 1280×720 derivative; not in installer |
| Local Aurora thumbnail WebP (outside repo) | 17,662 | 0.018 MB | 480×270 derivative; not in installer |
| Published application | 84,612,607 | 84.613 MB | CI BuildManifest `appBytes` |
| Installer EXE | 78,889,626 | 78.890 MB | CI BuildManifest `installerBytes` |
| Estimated installed application footprint | ~84,612,607 | ~84.613 MB | Published payload estimate only; not measured on a physical device |

The published application/installer sizes are not attributed to an asset library: the current build does not contain the generated Aurora package or the requested media collection.

## Manual acceptance still required

- Install and launch on a physical Windows 11 x64 machine.
- Confirm minimize, maximize, close, focus and window ownership behavior.
- Verify selected background visibility and contrast in the main player.
- Test GIF/video playback, pause, hide/resume, Reduced Motion, and missing/corrupt media on-device.
- Measure idle and playback memory after repeatedly switching high-resolution assets.
- Verify Focus/Karaoke, Mini Player and Game Mode remain visually correct.
- Validate actual audio-reactive visualization only after a real audio sample source is implemented and independently tested.
