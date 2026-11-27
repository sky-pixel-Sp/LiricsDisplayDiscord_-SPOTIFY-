# Phase 8 — Preferences, Themes and Accessibility

## Delivered
- Settings persist in the current user's Local AppData folder under SkyLyrics/settings.json.
- Writes use a same-directory temporary file followed by replacement.
- Preferences include Dark/Light theme, bounded synchronization offset (-500 to +500 ms), reduced motion, Discord Rich Presence and OBS loopback output.
- Invalid preferences are normalized safely; a malformed file falls back to defaults without crashing startup.
- Settings UI validates offsets, ports and Discord Application IDs before saving.
- Dark and Light theme resources use dynamic WPF resources, so the selected palette updates in the running application.
- Reduced-motion preference disables main lyric/artwork transitions while still respecting Windows client-area animation settings.

## Privacy
- No login tokens or secrets are stored in the preferences file. A Discord Application ID is public application configuration.
