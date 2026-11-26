# SkyLyrics Desktop — UI / Figma Specification

**Figma file:** https://www.figma.com/design/MypRaq6fzdDgPdyR1jbAFF  
**Target UI:** Windows 11 desktop, WPF  
**Design status:** Screen specification authored; editable Figma layers are pending because the connected Figma Starter plan reached its tool-call limit during this session.

## 1. Visual direction

Create a premium music companion, not a cyberpunk dashboard. Use a calm editorial music aesthetic: charcoal or warm-white surfaces, restrained accent color, clear hierarchy, generous spacing, and subtle motion. The app should feel like a native Windows desktop product.

- Typography: Segoe UI Variable / Segoe UI, with clear numeric playback typography.
- Iconography: consistent vector icons, preferably Lucide-style outline icons.
- Surfaces: layered panels with subtle borders; avoid excessive glass blur.
- Accent: muted mint/teal for the active lyric and progress; theme-aware contrast.
- Dark theme: deep charcoal/navy surfaces, soft-white primary text, muted slate secondary text.
- Light theme: warm off-white background, white surfaces, dark charcoal text, muted slate secondary text.
- Active lyric must be distinguishable by weight, opacity, and an accent marker—not color alone.

## 2. Design canvas and responsive behavior

Primary desktop frame: 1440 × 900.  
Compact validation frame: 1024 × 720.  
Minimum supported application window target: 900 × 620; below this, collapse navigation to icons and reduce artwork footprint rather than clipping lyrics.

Use auto-layout for all major layout containers. Define reusable components for navigation item, icon button, playback control, lyric row, status pill, settings row, and source badge.

## 3. Screen 1 — Main Dashboard

### Layout
- Left navigation rail: 220 px expanded, 72 px compact.
- Main content: flexible center column.
- Top header: app title, connection state, settings entry.
- Track identity row: album art, title, artist, album, source/sync badge.
- Lyrics panel: previous line, active line, next two lines; centered and vertically balanced.
- Footer: timeline/progress, elapsed and remaining duration, playback status.

### Content hierarchy
1. Active lyric is the strongest visual element.
2. Track title and artist are second.
3. Previous/next lyric lines are supporting context.
4. Source and sync capability remain visible but quiet.

### States
- Spotify connected and playing.
- Spotify connected and paused.
- Spotify disconnected.
- Loading lyrics.
- Synced lyrics.
- Unsynced lyrics.
- No verified match.
- Ambiguous candidates.

## 4. Screen 2 — Lyrics Focus / Karaoke

- Full-window presentation with minimal chrome.
- Track title and artist in a compact header.
- Active lyric centered with large type.
- Previous and upcoming lines visible with controlled opacity.
- Word-level highlight only when source has word timestamps.
- Optional hide-controls timeout must not hide exit affordance.
- Reduced-motion mode replaces sliding transitions with immediate state changes or simple opacity.

## 5. Screen 3 — Floating Mini Player

- Compact resizable window with a clear drag region.
- Album thumbnail, short track title, artist, active lyric, and sync state.
- Always-on-top is a user-controlled setting.
- Provide a visible close and return-to-main action.
- Avoid tiny text; enforce minimum dimensions.
- Do not add Spotify transport controls in v1.

## 6. Screen 4 — Settings

Sections:
- Appearance: Dark, Light, System.
- Lyrics synchronization: offset slider from -500 ms to +500 ms, numeric readout, reset.
- Animation: intensity and reduced motion.
- Window behavior: always on top, start minimized (only if startup support is implemented).
- Lyrics sources: LRCLIB status, local LRC import.
- Cache: clear cached lyrics with confirmation.
- Diagnostics: open diagnostics view.

## 7. Screen 5 — Lyrics Diagnostics

Display:
- Spotify session detected / disconnected.
- Source application identity.
- Current track metadata and duration.
- Last playback timeline update.
- Lyrics provider status and last HTTP outcome.
- Selected lyric source and record ID.
- Match status: Exact, Strong, Ambiguous, No Match.
- Sync capability: Word, Line, Unsynced.
- User offset and active playback position.
- Cache status and age.

Do not display secrets, access tokens, full local paths, or lyric text in diagnostics.

## 8. Component inventory

| Component | Variants / states |
|---|---|
| NavigationItem | Default, Hover, Selected, Compact |
| IconButton | Default, Hover, Pressed, Disabled |
| StatusPill | Connected, Paused, Disconnected, Loading, Warning |
| SyncBadge | Word-synced, Line-synced, Unsynced, No lyrics |
| LyricLine | Previous, Active, Upcoming, Inactive |
| TrackHeader | Artwork loaded, Artwork unavailable |
| ProgressTrack | Playing, Paused, Seeking |
| SettingsRow | Default, Focused, Disabled |
| CandidateCard | Selected, Unselected, Ambiguous |
| EmptyState | No session, No match, Provider unavailable |

## 9. Interaction rules

- Selecting a lyric candidate updates the active track's lyrics only after the user confirms.
- Changing offset updates the active lyric calculation immediately and can be reset to 0 ms.
- Theme selection updates every view, including floating and karaoke windows.
- Switching to karaoke must preserve current playback position and active lyric.
- Opening diagnostics must not trigger another lyrics lookup.
- A stale response for a previous track must never replace the current lyrics.

## 10. Animation specification

- Active line transition: short opacity + subtle vertical movement.
- Word progress: per-word highlight state driven by timestamps; do not use a looping animation as a timing source.
- Auto-scroll: smooth, interruptible, and disabled by reduced-motion.
- Artwork ambience: optional, low-cost, static or gently changing; no expensive real-time blur requirement.
- Theme transition: subtle surface/text color transition only if it remains legible.
- No flashing, strobing, or rapid pulsing.

## 11. Figma delivery checklist

- [ ] Main Dashboard dark and light frames.
- [ ] Lyrics Focus dark and light frames.
- [ ] Floating Mini Player dark and light frames.
- [ ] Settings dark and light frames.
- [ ] Lyrics Diagnostics dark and light frames.
- [ ] Reusable components and variants.
- [ ] Typography and color styles/variables.
- [ ] 1024 × 720 compact layout.
- [ ] 900 × 620 minimum-window layout.
- [ ] Prototype navigation between screens.
- [ ] Visual review for contrast, clipping, and hierarchy.

The Figma file exists, but these editable deliverables are not marked complete until the layers and components have actually been created and visually inspected.
