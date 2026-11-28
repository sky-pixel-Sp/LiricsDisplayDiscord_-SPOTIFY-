# SkyLyrics Desktop 2.3 — Continuation Handoff

## Current repository state

- Repository: `sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-`
- Working branch: `feature/skylyrics-2.3-real-assets`
- Base branch: `feature/skylyrics-2.2-perfecting`
- Audited branch head: `f09976d100827e2788d712d329fae41ea39277b1`
- The working branch existed before implementation and pointed at the same commit as the base branch.
- No project files have been changed by the P0 audit package.

## Completed

1. Confirmed repository, branch names, baseline head, solution layout, WPF target, Windows CI and installer pipeline.
2. Inventoried the 2.2 visuals. The repository contains SVG scenes, SVG textures, SVG note/icon art, fonts and HLSL sources, but no raster background or animated media library.
3. Recorded preserved functional boundaries, especially GSMTC timeline and lyric synchronization.
4. Queried 21st.dev for native-product visual references. The references are inspiration only and will be adapted to WPF.
5. Added P0 inventory, license register, initial catalog status, and implementation status.

## Outstanding work

- P1: create and commit genuinely distinct raster background masters and optimized variants.
- P2: produce actual animated media files with a documented duration/frame rate and reduced-motion alternative.
- P3: produce visualizer/texture/artwork files and clearly separate audio-reactive data from decorative timeline motion.
- P4: implement filesystem discovery, generated catalog, preview, filtering, favorites, and persistence.
- P5: integrate the Asset Library and selected background into existing WPF windows.
- P6: add decode/cache/memory controls and visibility-aware animation lifecycle.
- P7: add and run tests; execute Windows x64 installer workflow.
- P8: inspect artifacts and perform manual Windows 11 acceptance.

## Test evidence

- P0 is a source-tree inspection only.
- No build, unit-test, installer, memory, animation-playback, or device-acceptance test has been run for 2.3.
- Do not mark any package verified until its specific test evidence is available.

## Exact next action

Implement the asset metadata/discovery service and the native WPF Asset Library window on this branch, preserving the existing lyrics Library window as a separate destination. Add unit tests for catalog discovery, stable IDs, file size, SHA-256, malformed media handling, and missing-file behavior. Then add the first approved raster master and its display/thumbnail derivatives, generate the catalog from those exact files, update this handoff with the resulting commit, and run Windows CI.
