# Phase 12 — SkyLyrics Desktop 2.0 Premium UI Rebuild

## Product direction

SkyLyrics is a music companion for Windows, not a monitoring dashboard. The interface uses a calm editorial music aesthetic: deep charcoal surfaces, a restrained sage accent, crisp typography, real session artwork, and clear spacing. It deliberately avoids neon/cyber styling, fake album covers, fabricated playback states, and decorative charts that do not represent live data.

## Screens rebuilt

- **Main workspace:** persistent product header, navigation rail, central lyric stage, full-lyrics reading surface, and a right-side Now Playing/session panel.
- **Focus/Karaoke:** full-screen lyric composition with track context, previous/current/next lines, source sync status, and a visible exit affordance.
- **Mini Player:** compact movable window showing real artwork when supplied, track metadata, current lyric, and sync position.
- **Settings:** grouped appearance, synchronization, Discord Rich Presence, OBS Browser Overlay, and Game Mode configuration.
- **Diagnostics:** six live runtime measurements surfaced as a consistent card grid.
- **Game Mode:** retains the live synchronized overlay and its drag/resize/pin behavior, with a quieter premium color treatment and consistent typography.

## Design system

| Token | Dark | Light |
|---|---|---|
| Window | #0B0D10 | #F0F1ED |
| Surface | #12161B | #FAFBF8 |
| Raised surface | #1B2229 | #EFF2ED |
| Border | #28323A | #D9E0D8 |
| Accent | #A8E6CF | #397B64 |
| Primary text | #F4F5F2 | #17221D |
| Secondary text | #A8B1B6 | #53645B |

Shared WPF styles define buttons, cards, navigation items, fields, and eyebrow labels. SVG icons are stored as vector assets and are rendered through SharpVectors. Album artwork is only displayed when supplied by the Windows media session; the UI does not synthesize substitute artwork.

## Interaction and motion

- Main window keeps native Windows resize/minimize/maximize behavior.
- F11 toggles fullscreen and ESC restores the prior window state.
- Focus, Mini Player and Game Mode consume the same ShellViewModel/SceneState.
- Game Mode remains movable, resizable and optionally topmost.
- Existing global hotkeys and tray actions remain intact.
- Active lyric transitions retain the existing reduced-motion preference and system animation preference.
- No animation is used as a playback or synchronization clock.

## Verification boundary

The Windows CI workflow is the authority for WPF compilation, unit tests, self-contained win-x64 publishing and installer creation. Manual validation remains required for real Spotify playback, Windows media sessions, multiple monitors, display scaling, and the visual result on the user's laptop. This phase must not claim those device checks have happened unless they are actually performed.