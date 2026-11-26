# Discord Rich Presence

SkyLyrics Desktop can publish the current Spotify Desktop track to a user's Discord profile through Discord's local Windows IPC interface.

## First-time setup

1. Open the Discord Developer Portal: https://discord.com/developers/applications
2. Create an application named **SkyLyrics Desktop** (or a name you prefer).
3. On the application's **General Information** page, copy **Application ID**.
4. In SkyLyrics Desktop, click **Set up** or the Discord settings gear, then paste the numeric Application ID.
5. Keep the Discord desktop client running and enable activity sharing in Discord's Activity Privacy settings.

Only the public Application ID is saved, at %LOCALAPPDATA%\SkyLyrics\discord-app-id.txt. Do not paste a bot token, client secret, password, or OAuth token into SkyLyrics.

## Presence behavior

- Activity type: Listening.
- Details: the current Spotify track title.
- State: the current artist; a paused track is marked Paused.
- Playback timestamps: derived from Windows GSMTC position and duration. The timer is omitted while paused.
- Track changes update the activity. Discord IPC is retried after the Discord client is restarted.
- Presence is cleared when Spotify has no current track, when the user disconnects, and when SkyLyrics closes.

SkyLyrics does not publish lyric text, access Discord messages, join voice channels, send messages, control Discord, or capture/record audio. Spotify remains the playback controller.

## Requirements and limitations

- Windows desktop build of SkyLyrics.
- Discord Desktop must be running on the same Windows user session.
- The Discord application must have a valid Application ID.
- The user's Discord activity-sharing/privacy settings must allow activity to be shown.

The Discord application registration is user-owned configuration. SkyLyrics does not embed a fabricated Application ID and does not need a bot account or user OAuth login for basic local Rich Presence.
