# SkyLyrics Desktop 2.1 — Full Upgrade

Status: implementation baseline and acceptance contract  
Target: Windows 11 x64 · C# 14 · .NET 10 · WPF  
Parent baseline: SkyLyrics Desktop 2.0, commit `fc047fbc8476ec97a95a3c8bef59158265aa06d1`

## Product objective

SkyLyrics 2.1 expands the existing Spotify companion into a synchronized lyrics and visual presentation workspace. The upgrade is additive: preserve the 2.0 playback/session pipeline, lyric provider/cache, Discord IPC, OBS loopback server, Game Mode, hotkeys, installer and compact-window behavior. The 2.1 work is delivered in independently testable slices; no slice is considered complete because its UI merely renders.

## Non-negotiable product invariants

1. **One playback clock.** Windows GSMTC remains the source of playback metadata and timeline. All lyric surfaces and outputs consume the same immutable `SceneState`; no parallel polling clock is introduced.
2. **Evidence-backed synchronization.** Word karaoke requires source word timestamps. Line sync requires line timestamps. Plain text remains explicitly unsynchronized. No estimated word timings, fabricated progress, fake waveforms or invented metadata.
3. **User-owned content.** Imported, edited, translated and custom theme data is stored locally and can be exported. A translation is visibly identified by language and provenance; machine-generated text is never silently presented as source lyrics.
4. **Privacy by default.** No Spotify credentials, private Spotify endpoints, DRM circumvention, audio loopback capture, microphone capture, account scraping or remote control of unrelated applications. Discord continues to use local IPC and public Application ID. OBS remains loopback-only by default.
5. **Graceful degradation.** Effects, artwork processing and visualization can be reduced or disabled. On low-memory/integrated-GPU systems, the application must remain responsive and must not require a discrete GPU.
6. **No false integration claims.** A connector is shown as connected only after a real protocol handshake or a successful operation. Unsupported third-party actions remain unavailable rather than simulated.

## Upgrade workstreams

### 1. Advanced Lyrics Engine

- Add a dual-track lyric model: original document plus optional translation document, each retaining its own language, provider/source, sync mode and diagnostics.
- Add explicit lyric variants: original, translation, side-by-side and karaoke. Preserve the original text and timestamps when switching presentation modes.
- Add a local lyric editor for line text and timestamps. Edits are validated, undoable within the editing session, and saved as an exportable LRC/Lyricsfile document. Editing timestamps must not silently fabricate word-level timing.
- Add a timestamp calibration workflow with bounded offset, preview against the live GSMTC clock, and save/cancel semantics. Reuse the existing scheduler offset rather than creating another clock.
- Improve candidate selection diagnostics: expose match confidence and source metadata; never auto-accept ambiguous matches as exact.
- Add translation provider abstraction behind opt-in user action. Translation network requests must be cancellable, rate-limited, attributed and cached separately from source lyrics. A missing provider/key must not break core lyrics.
- Support import/export for LRC and the existing Lyricsfile YAML format. Validate malformed and oversized files before replacing the active document.

**Acceptance:** unit tests cover dual-document switching, language labels, source timestamp preservation, editor validation, offset bounds, cancellation and cache separation. Word karaoke is displayed only when the selected source document actually contains word timing.

### 2. Cinematic Visual Engine

- Introduce a rendering layer driven by immutable scene updates and an optional audio-analysis input contract. GSMTC metadata alone is not an audio signal; it must not be mislabeled as audio-reactive input.
- For real audio reactivity, implement an explicit Windows audio-session capture capability only after a privacy-visible opt-in, with device/session selection, clear capture indicator, stop control, and no recording or persistence. If no supported session stream is available, show a metadata-reactive visual mode and label it accurately.
- Provide GPU-optional visual modes: album-art ambient backdrop, gradient field, restrained particle field, and waveform/spectrum only when real sampled audio data is available.
- Animate album artwork only from the actual media-session thumbnail. No generated substitute cover or remote image scraping.
- Add a quality governor: frame-rate cap, effect quality, particle budget, reduced-motion handling, visibility pause, and automatic fallback when rendering becomes expensive.
- Use WPF composition/Direct2D-compatible primitives already supported by the app stack; avoid adding a heavy game engine dependency for a desktop overlay.

**Acceptance:** deterministic renderer tests use recorded synthetic test signals only inside tests; production UI never displays synthetic test data. Verify reduce-motion, hidden-window pause, no-artwork fallback, low-quality mode and no-audio-source state. Measure CPU/memory on the supported integrated-GPU baseline.

### 3. Advanced Integrations

- **Spotify:** retain Windows GSMTC as the supported desktop playback integration. Improve session selection/reconnect and explain that browser playback may not expose the selected Windows media session. Do not request Spotify account credentials.
- **Discord:** harden local IPC lifecycle, handshake/reconnect, clear-on-disable, and activity mapping. No bot token or user token.
- **OBS:** retain loopback-only HTTP service, add overlay profiles and validated presentation options. Keep output text-safe and bounded; no arbitrary file serving or remote bind by default.
- **TikTok:** treat this as a constrained companion integration, not a promise of controlling TikTok. Support only documented/public share or user-initiated import flows that are available and permitted. Do not scrape private endpoints, automate likes/follows/comments, collect cookies, or bypass platform access controls. If the platform offers no supported API for a requested operation, expose a clear unsupported state.
- **Desktop overlays:** expose an integration contract for local overlay targets and global shortcut conflicts. Integrations are opt-in, independently enabled and independently diagnosable.

**Acceptance:** integration contract tests cover disabled state, unavailable target, successful handshake, disconnect, reconnect, invalid configuration and shutdown cleanup. No connected badge without verified connection.

### 4. Customization Studio

- Add typed, versioned theme profiles: colors, typography, spacing, lyric alignment, artwork treatment, background/effect settings and animation intensity.
- Provide built-in Light, Dark and Sage profiles as actual packaged themes, plus user-created profiles. No blank preview cards or placeholder themes.
- Add layout presets for main stage, Focus/Karaoke, Mini Player, Game Mode and OBS. Keep layout values bounded and recoverable.
- Add font selection from installed Windows fonts with safe fallback; do not download fonts automatically.
- Add animation presets that map to the shared motion system and respect Reduce Motion.
- Add theme import/export with schema version, strict size limits, safe parsing and migration. Do not execute imported content as code.
- Add preview/apply/cancel/reset flows; persist only after explicit Apply.

**Acceptance:** tests cover schema validation, migrations, invalid profile rejection, import/export round-trip, font fallback, reset and persistence. Layouts remain usable at compact window sizes and standard Windows DPI scaling.

## Architecture boundaries

- `SkyLyrics.Core`: immutable lyric/scene domain contracts and validation only.
- `SkyLyrics.Application`: use cases, provider contracts, orchestration and settings contracts; no WPF dependencies.
- `SkyLyrics.Infrastructure`: LRCLIB/translation providers, SQLite persistence, Discord IPC, safe theme/profile persistence.
- `SkyLyrics.Infrastructure.Windows`: GSMTC and any explicitly opted-in Windows audio-session capability.
- `SkyLyrics.Desktop`: WPF views, editor, customization studio, visual presentation and interaction.
- `tests/SkyLyrics.Tests`: domain, application and infrastructure contract tests. UI-specific behavior remains covered by Windows CI and manual acceptance.

Do not add a second playback clock, bind view code directly to external APIs, or let a slow visualization/connector block `SyncScheduler`.

## Phased delivery

| Phase | Deliverable | Exit gate |
|---|---|---|
| 13A | Domain contracts, preference schema, migrations and test fixtures | Existing 2.0 behavior preserved; tests pass |
| 13B | Dual-language lyrics, editor and calibration | Import/edit/export round-trip; no invented timestamps |
| 13C | Visual engine and quality governor | Real source labeling; reduced-motion and low-resource tests |
| 13D | Discord/OBS/Spotify hardening and supported integration surface | Protocol/contract tests; no false connected states |
| 13E | Customization Studio and theme/layout persistence | Safe import/export, migrations, compact layout tests |
| 13F | Windows acceptance, installer, docs and release candidate | Full CI, manual Spotify/Discord/OBS/DPI acceptance |

## Performance and compatibility budget

- Keep the application x64 and self-contained Windows installer flow.
- Prefer event-driven scene updates and bounded queues over high-frequency polling.
- Visualization frame rate is capped and pauses when its surface is hidden.
- All optional visual effects must have a no-animation / low-quality mode.
- Avoid large runtime dependencies unless they deliver a necessary capability and pass package/security review.
- Preserve existing keyboard shortcuts unless a documented conflict is resolved.
- Test at compact window sizes as well as the primary desktop layout; do not force a large minimum window.

## Definition of done

A workstream is complete only when its implementation is committed, tests are added and passing, CI has compiled/published/packaged the Windows x64 app, documentation matches actual behavior, and any hardware-/account-dependent behavior is explicitly marked as requiring manual acceptance. A design mockup, documentation-only change, successful compile, or green CI alone is not proof that a third-party integration works on the user's machine.

## Explicitly out of scope

- Spotify playback transport control where Windows GSMTC does not expose it.
- Fabricated word timings, lyrics, translation or artwork.
- Unofficial/private TikTok or Spotify APIs, cookie extraction, credential collection or platform-control automation.
- Publicly exposed OBS server by default.
- Always-on microphone/audio capture, recording or uploading.
- Executable theme files, downloaded fonts without consent, or arbitrary plugin code.
