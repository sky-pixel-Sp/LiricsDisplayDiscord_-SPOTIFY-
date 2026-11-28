# SkyLyrics Desktop 2.2 — Refinement Status

**Release line:** 2.2.0-preview.1 (CI builds append the workflow run number)  
**Working branch:** `feature/skylyrics-2.2-perfecting`  
**Starting commit:** `3cec7bbd02192e2edb6ad88a6a89216f5e26ea85`  
**Target:** Windows 11 x64 · C# 14 · .NET 10 · WPF

## Refinements in this change

- Adaptive quality keeps the user's selected tier as its hard ceiling.
- Sustained frame intervals above 20 ms downgrade one tier after a two-second measurement window.
- Recovery is deliberately slower: three consecutive two-second windows averaging 14 ms or less are required before moving up one tier.
- A moderate window resets the recovery counter. This hysteresis prevents rapid switching around one threshold.
- Measurement windows with fewer than ten samples are ignored.
- A user-selected Reduced tier stays locked. An automatic downgrade to Reduced continues low-cost measurement and repaints once every three display frames, allowing recovery without restoring full visual cost.
- The Windows CI version identity is aligned to 2.2.
- The Windows Release workflow uses one resolved version for assembly metadata and the installer, finds the actual versioned Inno Setup output, and stores the matching checksum and build manifest.

## Visual reference

21st.dev inspiration search was used as a visual reference for compact waveform-player and synchronized lyric-caption patterns. The result is adapted to native WPF; React/shadcn components are not inserted into the desktop application.

## Verified baseline

- The starting branch head was `3cec7bbd02192e2edb6ad88a6a89216f5e26ea85`.
- Windows CI run #253 completed successfully before this refinement.
- That run produced `skylyrics-windows-x64-installer` and `skylyrics-windows-test-results` artifacts.

## Known product boundaries

- The no-lyrics wave is driven by the playback timeline and is not an audio-reactive spectrum.
- Audio capture/loopback remains outside this product's locked specification.
- GSMTC browser sessions identify the browser media source, not necessarily the website. YouTube Music website identity must not be inferred from browser metadata alone.
- Successful Windows CI does not replace manual acceptance against a real Spotify/browser/Windows Media session, Discord IPC, multiple monitors, display scaling and GPU performance.
- The installer is unsigned unless a valid code-signing certificate is configured.

## Release gate

This refinement is not declared complete until the new Windows CI run passes, its installer and test-results artifacts exist, and the manual acceptance items above are checked on Windows 11.
