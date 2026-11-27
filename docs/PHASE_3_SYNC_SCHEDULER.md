# Phase 3 — Event-driven SyncScheduler and SceneState

## Scope

Consumes the immutable GSMTC PlaybackSnapshot and the source-preserving LyricsDocument produced by Phases 1 and 2. It calculates the active lyric line and optional source-timed word state, then publishes immutable SceneState revisions for WPF and future output sinks.

## Clock and timing correctness

- PlaybackSnapshot.Position is already adjusted by the Phase 1 PlaybackClock for the age of the GSMTC source timeline.
- SyncScheduler anchors a fresh monotonic PlaybackClock at ReceivedAtUtc. This advances from the receipt time exactly once and prevents timeline age from being counted twice.
- Only Playing advances. Paused, Stopped, Unknown, and disconnected states never advance.
- Timeline start and end bounds are respected.
- User offset defaults to 0 ms and is clamped to the product range of -500 ms through +500 ms.

## Event-driven scheduler

- New playback snapshots re-anchor the monotonic clock after timeline, seek, pause/resume, and session events.
- A binary search selects the latest timestamped line at or before the effective playback position and the nearest upcoming line.
- Equal-timestamp source lines are grouped into a single scene line while preserving all source text.
- A one-shot TimeProvider timer is scheduled for the next line, word-start, or explicit word-end boundary. It is cancelled while playback is not Playing or no timed lyrics are loaded.
- No continuously running timer, frame polling loop, or network request is used for lyric synchronization.
- If a timer wakes slightly early, the scheduler calculates again and arms the remaining boundary delay.

## Word-level behavior

- Word highlighting is enabled only when the imported document contains source word timestamps.
- Source word end times are used for progress only when supplied and valid.
- A word with no source end timestamp can be identified as active but has no fabricated progress duration.
- Lines without timestamps, plain text lyrics, and instrumental documents never receive generated active-line timestamps.

## Scene publication

- Every published state is an immutable SceneState with an increasing revision.
- Scene changes are delivered through SceneChanged; a faulty subscriber cannot break the playback clock.
- Track changes clear the previous document's active line immediately. Disconnect clears track, lyrics, playback status, and position.
- The WPF shell displays active line, upcoming line, source-timed active word, and synchronization clock status. The full lyrics text remains available for reading.

## Automated validation

SyncScheduler tests cover line activation before/at/after boundaries, binary-search next-line selection, event-driven one-shot timers, pause, seek, playback-rate scheduling, bounded offsets, timeline-start clamp, word progress with and without explicit end times, unsynced documents, grouped timestamp collisions, disconnect, track replacement, and monotonic SceneState revisions.

## Manual validation still required

- Confirm actual Spotify Desktop timeline updates, seeks and pause/resume behavior on Windows 11.
- Compare visible line-boundary timing against the Spotify playback progress on several real tracks.
- Validate real word-synced Lyricsfile data on a track that has word timings; LRCLIB line-synced LRC must remain line-only.
- Check the active-line panel at the target compact and full window sizes.
- Visual layer authoring in the linked Figma file is blocked in this session by the connected Figma Starter plan tool-call limit; no Figma design update is claimed.

## Exit criteria

- [x] Event-driven SyncScheduler with monotonic interpolation and one-shot boundary timers.
- [x] Binary-search active line and upcoming line selection.
- [x] Pause, resume, seek, track replacement, disconnect, and timeline bounds handled.
- [x] Bounded user synchronization offset.
- [x] Word state uses only source-provided word boundaries.
- [x] Immutable SceneState revisions wired into the WPF shell.
- [x] Deterministic scheduler test suite added.
- [ ] Live Windows 11 / Spotify Desktop integration validation.
- [ ] Visual sign-off in Figma and the running desktop application.

## Next phase

Phase 4 adds the cinematic WPF lyrics renderer, artwork presentation from the actual GSMTC thumbnail where available, motion curves and word-highlight transitions. Album art must be sourced from the current media session or an explicitly selected local asset; generic or invented cover images are not used.
