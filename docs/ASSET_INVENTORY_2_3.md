# SkyLyrics Desktop 2.3 — Asset Inventory (P0)

**Repository:** `sky-pixel-Sp/LiricsDisplayDiscord_-SPOTIFY-`  
**Base branch:** `feature/skylyrics-2.2-perfecting`  
**Working branch:** `feature/skylyrics-2.3-real-assets`  
**Audited base commit:** `f09976d100827e2788d712d329fae41ea39277b1`  
**Audit date:** 2026-11-28

## Repository facts

The requested 2.3 working branch already existed and pointed to the same commit as the supplied 2.2 baseline. No branch recreation or history rewrite was necessary.

The solution is a native Windows desktop solution:
- C# language version 14.
- .NET 10 Windows target.
- WPF desktop UI.
- x64 platform target.
- Existing Windows CI and Inno Setup packaging workflows.

## Existing asset inventory

| Area | Existing files | Finding |
|---|---:|---|
| Backgrounds | 6 SVG | Vector scenes only; not a raster photographic library |
| Textures | 4 SVG | Vector texture/effect approximations |
| Notes | 3 SVG | Scalable note symbols |
| Icons | 10 SVG | Appropriate use of SVG for small UI symbols |
| Branding | 1 SVG + ICO | Existing application branding |
| Fonts | Poppins and Bebas Neue license files | OFL notices are present |
| Effects | 2 HLSL source files | Shader source, not media assets |
| Animated media | 0 discovered | No GIF/video/sprite loop assets found in the audited tree |
| Raster background library | 0 discovered | No qualifying PNG/JPEG/WebP photographic masters found |
| Raster visualizer library | 0 discovered | Existing waveform control is application-rendered |

Counts above are scoped to the audited source tree and file paths, not to generated outputs or developer workspaces.

## Existing feature boundaries to preserve

- GSMTC playback session monitoring and playback timeline.
- LRCLIB/local LRC lookup, parsing, cache, and match validation.
- `SyncScheduler` and its playback-time anchoring rules.
- Lyrics focus window, floating player, Game Mode, OBS overlay and Discord presence.
- User preferences persistence through the existing JSON preference store.
- Adaptive visual quality and reduced-motion handling.

The lyric scheduler must remain the sole authority for lyric position. Backgrounds, visualizer decorations, and animation timers must not feed time or fake audio samples into synchronization.

## Visual research

21st.dev inspiration search returned music-player and waveform-player patterns plus a dashboard/sidebar reference. These are design references only. The target remains native WPF; web React/shadcn components are not embedded into the desktop runtime.

## Asset readiness

P0 found no repository-resident raster or animated media suitable for the 2.3 library. A newly generated aurora landscape candidate exists in the current production workspace, but it has not yet been committed to this repository and is therefore excluded from the catalog and package-size accounting.

No claim is made that 4K masters, animation loops, or a 150–250 MB library exist at this point.
