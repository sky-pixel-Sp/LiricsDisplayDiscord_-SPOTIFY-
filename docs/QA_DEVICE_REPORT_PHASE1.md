# SkyLyrics Desktop — Real-Device QA Report (Phase 1)

**Test source:** User-reported manual test on a Windows laptop  
**Test date:** 2026-11-26  
**Build under test:** Phase 1 Windows x64 installer artifact from workflow run #45807615647  
**Overall result:** Partial pass — not release-ready

## User-reported results

| # | Area | Result | User report |
|---:|---|---|---|
| 1 | Installation | PASS | Installed successfully |
| 2 | Application launch | PASS | Opens normally |
| 3 | Spotify detection | PARTIAL | Detects Spotify, but playback updates stutter or skip |
| 4 | Lyrics retrieval | PASS | Lyrics appear |
| 5 | Lyric synchronization | FAIL | Does not follow playback accurately |
| 6 | Animation | FAIL | Animation does not work |
| 7 | Performance | FAIL | Application feels slow |

## Code-review findings

1. The Phase 1 WPF timer called the Spotify session reader on every tick. The session reader enumerated sessions and requested media properties repeatedly, even though title, artist, and album usually remain unchanged while a track plays.
2. The Phase 1 refresh cadence could be as short as 110 ms for word-timed lyrics. A slow GSMTC response could therefore delay UI work and make playback updates appear irregular.
3. Line/word highlighting requires real source timestamps. Plain-text lyrics are intentionally unsynchronized; the app must not invent timestamps to make those lyrics appear synchronized.
4. The user did not report the lyric source status label (LINE SYNC, WORD + LINE SYNC, or UNSYNCED), so it is not yet confirmed whether the failed sync test used timed lyrics or plain lyrics.

## Phase 2 candidate changes

- Replace high-frequency metadata polling with Windows GSMTC session, media-properties, playback-info, and timeline events.
- Read media metadata when a session or track changes, not on every render tick.
- Use the Windows timeline's LastUpdatedTime as the anchor for playback-position interpolation between authoritative timeline updates.
- Keep lyric rendering on a separate 30 FPS clock only when timed lyrics are actively playing; reduce idle/paused refresh frequency.
- Start imported timed lyrics at the current playback position instead of resetting them to zero.
- Add deterministic unit tests for playing, paused, future-anchor, and duration-clamping behavior.

## Acceptance criteria for the next laptop test

- Spotify detection remains stable through play, pause, resume, next-track, previous-track, and seek.
- Timed LRC lines change at their source timestamps without repeated skips.
- Word highlighting runs only when provider-supplied word timestamps are present and valid.
- Plain text remains clearly labeled UNSYNCED; no generated timestamps are allowed.
- Active-line transition animation is visible and auto-scroll does not jump unnecessarily.
- CPU and memory usage are measured while idle, while playing timed lyrics, and after switching several tracks.
- No unhandled exception appears during disconnect/reconnect or track changes.

## Verification status

The Phase 2 code is a candidate fix until the Windows x64 CI build and installer smoke test pass. It must then be installed and manually re-tested on the user's laptop. A passing CI run alone does not establish real-device performance or accurate synchronization.
