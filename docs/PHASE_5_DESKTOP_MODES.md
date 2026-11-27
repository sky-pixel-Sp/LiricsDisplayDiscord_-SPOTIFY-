# Phase 5 — Desktop Modes, Tray and Shortcuts

## Delivered
- Full-screen Lyrics Focus mode with Escape-to-exit, previous/current/upcoming lyric context and a large source-backed active line.
- Floating mini player sharing the same ShellViewModel, artwork instance and SyncScheduler as the main window.
- Windows notification-area icon derived from the Lucide headphones vector already used by the product; its SVG source and license remain in the repository.
- Tray menu for opening the dashboard, focus view, mini player, settings, diagnostics and explicit application exit.
- Main-window close hides to the tray. The process exits only through the tray Exit action.
- Global shortcuts: Ctrl+Alt+K (focus), Ctrl+Alt+M (mini player), Ctrl+Alt+S (restore dashboard).
- Shortcut collisions are reported and do not crash the application.
- No second playback monitor or independent lyric clock is created for auxiliary windows.

## Validation
- Windows x64 CI compiles the WPF and WinForms integration.
- Live shortcut registration and tray interaction still require an interactive Windows 11 acceptance run.
