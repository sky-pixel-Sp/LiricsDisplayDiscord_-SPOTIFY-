# Phase 7 — Discord Rich Presence

## Delivered
- Discord output is an independent IOutputSink fed from immutable SceneState.
- Uses the local Discord desktop IPC named-pipe protocol. It never requests Discord passwords, user tokens, browser cookies or account credentials.
- User supplies their own numeric Discord Application ID in Settings. Rich Presence remains disabled until explicitly enabled with a valid ID.
- Presence contains the actual Spotify track title and artist. Paused state is disclosed. Stopped or disconnected playback clears activity.
- IPC frames use little-endian opcode/length headers, a 1 MiB payload limit, bounded connection attempts and reconnect backoff.
- A bounded one-item scene queue keeps slow external output from accumulating unbounded memory or blocking lyric synchronization.

## Requirements
- Discord Desktop must be running.
- The user must create/configure an application in the Discord Developer Portal and enter its public Application ID.
- CI validates frame encoding/decoding, mapper behavior and clear-state behavior. Live IPC handshake requires Discord Desktop on Windows.
