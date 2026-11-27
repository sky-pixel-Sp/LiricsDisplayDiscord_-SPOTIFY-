# SkyLyrics Desktop 1.1 — window and Game Mode rebuild

## Desktop window behavior

- Main dashboard retains native Windows title-bar controls for minimize, maximize/restore, resize and close.
- Closing the main window exits SkyLyrics and disposes the tray icon and hosted services. It is not a hide-to-tray gesture.
- F11 toggles borderless fullscreen. ESC returns to the previous window style and state.
- The existing Focus/Karaoke window remains a separate full-screen lyric view.

## Game Mode overlay

Game Mode uses the same ShellViewModel and synchronized SceneState as the dashboard. It does not create an independent playback timer or fabricate lyric text.

The overlay is borderless and topmost by default. Drag its header to place it anywhere on the desktop, including another connected monitor. Eight edge/corner resize handles adjust its dimensions. Geometry is saved to the existing user settings file after movement or resize and restored the next time Game Mode opens.

### Direct controls

- Pin / unpin toggles always-on-top without closing the overlay.
- Settings opens the Game Mode configuration dialog.
- The close (×) control closes Game Mode immediately; Ctrl+Alt+G also toggles it.
- The main dashboard and notification-area menu both expose Game Mode.

### Configuration

The per-user settings file at %LOCALAPPDATA%/SkyLyrics/settings.json stores Game Mode position, width, height, opacity, always-on-top, gradient background, lyric text effect, and active-line animation. The configuration dialog constrains dimensions to 420–1920 × 112–600 pixels and opacity to 55–100 percent.

Background choices: Aurora Mint, Deep Ocean, Violet Haze, Sunset Ember and Graphite Glass.

Text effects: Clean, Mint Halo, Ice Blue and Soft Depth.

Active-line motion: Soft Fade, Cinematic Slide, Scale Reveal, Spring Motion and Quick Snap.

The application applies changed settings to an open Game Mode window immediately.

## Main window visual and motion behavior

- Static ambient radial color fields remain visible when Spotify does not supply artwork. Genuine Spotify artwork is still displayed when provided.
- The dashboard enters with a short eased fade/translate transition.
- Active lyric changes use eased opacity, translation and a restrained scale settle.
- The system Reduce Motion setting and Windows animation preference suppress those transitions.

## Validation boundary

The Windows CI workflow builds Release, runs automated tests, publishes the self-contained win-x64 application and compiles the Inno Setup installer. CI does not replace manual acceptance on a Windows 11 desktop with Spotify Desktop, multiple monitors, DPI scaling, minimize/maximize/fullscreen, overlay resize/movement, close, and idle/playing resource measurements.
