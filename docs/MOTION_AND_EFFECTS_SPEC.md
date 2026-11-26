# SkyLyrics Desktop — Motion & Effects Specification

## Motion profile

SkyLyrics uses a restrained motion system: strong feedback on meaningful state changes, no constant decorative motion across the entire window.

## Implemented interactions

| Interaction | Motion | Trigger |
|---|---|---|
| App launch | Window fade + short upward reveal, staggered card entrances | Window loaded |
| Player card | Subtle hover scale and soft static depth | Pointer hover |
| Lyrics card | Subtle hover scale | Pointer hover |
| Buttons | Hover lift, pressed compression, focus border | Pointer / keyboard |
| Spotify connection | Small connection indicator pulse | Session connects |
| Playback | Four-bar equalizer and playback-dot pulse | Spotify playing state |
| Track switch | Track title and artist fade/slide | Track identity changes |
| Active lyric | Accent surface, text color/size, opacity and vertical transition | Active timestamp changes |
| Word sync | Color transition on the currently active source-timed word | Word timestamp changes |
| Auto-follow | Ease-out scroll to the active lyric line | Active line changes |
| Theme switch | Window background color transition | Theme toggle |
| Import success | Brief accent flash on the import action | Successful file import |

## Performance guardrails

- No looping animated background, full-window blur, particles, or continuously moving decorative gradients.
- The equalizer is limited to four tiny transforms and only runs while Spotify reports active playback.
- The connection pulse runs only while a Spotify session is connected.
- Lyric line transitions animate only the incoming active line and the line that just became inactive. Other lines receive direct state updates.
- Word synchronization animates only the outgoing and incoming word runs; it does not rebuild the lyric tree.
- Smooth scrolling is transient (about 280 ms) and stops immediately when complete.
- The render clock remains separate from Spotify session metadata acquisition.
- Card hover effects are transform-only and do not alter layout measurement.
- Ambient color fields are static gradients, not frame-by-frame animations.

## Accessibility

- Keyboard focus receives a visible accent outline.
- Button pressed states return to the hover state correctly when the pointer remains over the control.
- Motion is limited to small translation, opacity, scale and color changes; no flashing or rapid strobing effects are used.
- Lyric text and synchronization status remain readable without relying on animation alone.

## Integrity

- Playback remains controlled by Spotify Desktop.
- Word highlighting is shown only where the lyric source provides valid word timestamps.
- Unsynchronized text remains labeled UNSYNCED.
- Visual states do not imply a feature that is not implemented.