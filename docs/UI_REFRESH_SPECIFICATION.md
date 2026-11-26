# SkyLyrics Desktop — UI Refresh Specification

## Direction

A calm, premium Windows desktop companion for Spotify lyrics. The interface prioritizes the active song and readable lyrics rather than dashboard widgets or decorative effects.

## Layout

- Header: SkyLyrics identity, live Spotify connection status, and a single theme toggle.
- Now Playing surface: track title, artist, playback state, current time, and progress.
- Lyrics surface: source/synchronization status, a visible import action, and a large scrollable lyric reading area.
- Footer: concise accuracy/privacy invariant and platform label.

## Visual system

### Dark theme
- Window: #0C1210
- Surface: #131C19
- Elevated surface: #19241F
- Stroke: #26352E
- Primary text: #EEF5F1
- Secondary text: #A7B6AE
- Muted text: #73847B
- Accent: #67D7B0
- Accent soft: #1D382E

### Light theme
- Window: #F2F6F3
- Surface: #FFFFFF
- Elevated surface: #F7FBF8
- Stroke: #DEE8E1
- Primary text: #18251F
- Secondary text: #52645A
- Muted text: #74857B
- Accent: #16865E
- Accent soft: #E2F4EA

## Interaction rules

- The theme toggle changes semantic application resource tokens, not individual decorative elements.
- Import Lyrics keeps the existing file import behavior.
- The Spotify status is informational; playback remains controlled by Spotify.
- Lyrics source status remains visible (WORD + LINE SYNC, LINE SYNC, UNSYNCED, etc.).
- No inactive navigation items or controls are presented as if they work.

## Performance and accessibility

- Use native WPF controls and lightweight solid-color surfaces.
- Avoid continuous decorative animation, blur, heavy drop shadows, or animated backgrounds.
- Keep keyboard focus visible through standard WPF focus behavior.
- Use Segoe UI Variable, clear contrast, readable lyric sizing, and generous spacing.
- Maintain a minimum supported window size of 900x620 and use layout sizing rather than fixed element positions.

## Content integrity

- Do not display fake album art or placeholder data as real Spotify metadata.
- Do not create lyric text or timestamps.
- Plain lyrics must remain labeled UNSYNCED.
- Word highlighting is displayed only when source word timestamps are valid.

## Figma status

The existing Figma file was identified, but Figma MCP returned a Starter-plan tool-call limit error before the file could be inspected or updated. No new frames or edits are claimed as completed in Figma. This specification is the current source of truth for the implementation until Figma access is available again.
