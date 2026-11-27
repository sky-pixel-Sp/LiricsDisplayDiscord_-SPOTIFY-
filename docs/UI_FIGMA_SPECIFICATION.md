# SkyLyrics Desktop — UI / Figma Specification 2.0

**Figma file:** https://www.figma.com/design/MypRaq6fzdDgPdyR1jbAFF  
**Target:** Windows 11 x64 · C# / .NET 10 / WPF  
**Direction:** premium editorial music companion

## Visual system

SkyLyrics should feel like a focused music product rather than an operations dashboard. Use calm charcoal surfaces, a restrained sage accent, confident typography, large meaningful lyric typography, and generous spacing. Avoid cyberpunk/neon gradients, overuse of glass blur, tiny all-caps labels, fake album covers, fabricated playback controls, and decorative visualizers that do not represent real playback data.

- Dark canvas: #0B0D10; surfaces: #12161B and #1B2229; border: #28323A.
- Dark accent: #A8E6CF; primary text: #F4F5F2; secondary text: #A8B1B6.
- Light canvas: #F0F1ED; surfaces: #FAFBF8 and #EFF2ED; accent: #397B64.
- Typography: Segoe UI Variable Display / Segoe UI Variable with clear numeric playback labels.
- SVG iconography: Lucide-style outline assets stored in the application resource bundle.
- Motion: short eased opacity/translation transitions; honor the system animation setting and Reduce Motion preference.

## Main workspace

- Fixed left navigation rail for Live Lyrics, Focus, Mini Player, Game Mode, Full Screen, Import Lyrics, Diagnostics and Settings.
- Center column contains the page title, prominent lyric stage, and a compact full-lyrics reading surface.
- Right panel presents current Spotify artwork when GSMTC supplies it, title/artist/album metadata, playback position, session status and lyric source status.
- Main lyric hierarchy: previous line (quiet), active line (dominant), active word (only where genuine word timestamps exist), upcoming line (supporting context).
- Artwork absence must be represented honestly; never generate or substitute a fake cover.

## Companion windows

- Focus/Karaoke: full-screen, distraction-free lyric hierarchy with track context and a persistent ESC exit affordance.
- Mini Player: resizable floating window with real artwork, metadata, active lyric and synchronized position.
- Game Mode: separate movable/resizable topmost-capable overlay; preserve drag, resize, pin, saved placement, visual settings and global shortcut behavior.
- Settings: appearance, synchronization, Discord Rich Presence, OBS Browser Overlay and Game Mode configuration.
- Diagnostics: real process working set, managed heap, scene revision, artwork payload, playback state and scene timestamp.

## Responsive targets

- Primary design canvas: 1440 × 900.
- Compact validation: 1024 × 720.
- Minimum application frame: 980 × 610; panels must remain usable without lyric clipping.
- Avoid fixed-width content within the center column; text must wrap and full lyrics must scroll independently.

## Interaction invariants

- All lyric views consume the shared ShellViewModel/SceneState.
- Theme changes apply to every desktop window.
- Focus/Mini/Game Mode must not create a second playback or synchronization clock.
- Word-level treatment only appears when the source has word timestamps.
- Opening diagnostics must not initiate another lyrics lookup.
- Stale responses for an earlier track must not replace the current lyrics.
- Reduced motion removes or simplifies spatial transitions without affecting sync.

## Figma delivery boundary

This repository specification describes the intended design system and implemented WPF surface. The existing Figma file remains a separate editable-design deliverable; do not mark its frames, components, variables or prototype as complete until they have actually been created and visually inspected in Figma.