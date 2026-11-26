# SkyLyrics Desktop — Motion, Effects & Backgrounds

## Independent appearance catalogs

Appearance Studio exposes three separate selection lists. Each category has 30 individual presets; choosing one option does not consume or replace a choice in another category.

| Category | Count | Applied to |
|---|---:|---|
| Animations | 30 | Active lyric transitions: opacity, position, scale, rotation, easing and duration |
| Effects | 30 | Active lyric text lighting and depth using performance-biased WPF effects |
| Backgrounds | 30 | Main window surface and Game Mode overlay using independent multi-stop gradients |

The selection is persisted at %LOCALAPPDATA%\SkyLyrics\appearance.json.

## Motion behavior

- App launch: window fade + short upward reveal, staggered card entrances.
- Player card and lyrics card: subtle hover scale.
- Buttons: hover lift, pressed compression, focus border.
- Spotify connection: small connection indicator pulse.
- Playback: four-bar equalizer and playback-dot pulse.
- Track switch: track title and artist fade/slide.
- Active lyric: selected animation preset, accent surface, selected text effect, text color and font-size transition.
- Word sync: color transition on the currently active source-timed word.
- Auto-follow: smooth scroll to the active line using its absolute content position.
- Manual scroll: auto-follow is paused for five seconds after mouse-wheel or supported keyboard scrolling.
- Game Mode: main window hides and a single current timestamped lyric line appears in a topmost overlay. Right-click exits Game Mode and restores the main window.

## Performance guardrails

- No looping animated background or continuously moving decorative gradients.
- Only the active lyric receives the selected text effect.
- Drop-shadow effects use WPF's performance rendering bias.
- Active-line transitions animate only the incoming active line and the line that just became inactive.
- Word synchronization animates only the outgoing and incoming word runs; it does not rebuild the lyric tree.
- Smooth scrolling is transient and stops immediately when the user manually scrolls.
- The render clock remains separate from Spotify session metadata acquisition.
- Card hover effects are transform-only and do not alter layout measurement.

## Accessibility and integrity

- Keyboard focus receives a visible accent outline.
- No flashing or rapid strobing effects are included in the animation catalog.
- Lyric text and synchronization status remain readable without relying on animation alone.
- Playback remains controlled by Spotify Desktop.
- Word highlighting and Game Mode's live line require actual source timestamps.
- Unsynchronized text remains labeled UNSYNCED and is not falsely presented as a currently timed lyric.
