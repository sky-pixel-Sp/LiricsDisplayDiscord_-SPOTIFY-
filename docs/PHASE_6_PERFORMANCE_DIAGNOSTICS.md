# Phase 6 — Performance Budget and Diagnostics

## Delivered
- A diagnostics window samples process working set and managed heap only while the window is open, at a three-second interval.
- Displays the immutable scene revision, playback status, last scene update and actual artwork payload size.
- The artwork pipeline remains memory-only, skips payloads above its 8 MiB acquisition cap, and decodes to a 512-pixel WPF bitmap.
- No fabricated CPU percentage, prediction or estimated performance score is presented.
- Closing the diagnostics window stops its sampling timer.

## Acceptance boundary
- CI validates compilation and scene-backed diagnostics access.
- The under-200 MB idle-memory requirement is a target, not a measured result. It must be measured on a Windows 11 device after clean launch and prolonged playback.
