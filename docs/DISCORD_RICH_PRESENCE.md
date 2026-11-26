# Discord Rich Presence

## Activity behavior

SkyLyrics publishes the currently active Spotify Desktop track to the user's Discord profile:

- Activity type: Listening.
- Details: Spotify track title.
- State: active synchronized lyric line, with artist metadata when the field limit permits it.
- Timestamps: based only on Windows GSMTC playback position and duration.
- Updates: whenever the active timestamped lyric line changes.
- Clears: when Spotify has no active track, when the user disconnects, or when SkyLyrics exits.

Only lyric lines with source timestamps are published as live lyrics. Plain text and other unsynchronized sources are not assigned an invented current line.

## Requirements and setup

A Discord application must be registered in the [Discord Developer Portal](https://discord.com/developers/applications). Copy its public Application ID from General Information and save it in SkyLyrics. Discord Desktop must be running and activity sharing must be enabled.

No bot token, Discord password, or user OAuth login is requested or stored.

## Protocol notes

Discord's current documentation describes a desktop Rich Presence path that can be used without calling the Social SDK authentication flow. The direct method sets the registered Application ID and updates Rich Presence through RPC while the Discord desktop client is running:

- [Discord Social SDK guide — Rich Presence without authentication](https://github.com/discord/discord-api-docs/blob/main/developers/discord-social-sdk/development-guides/setting-rich-presence.mdx)
- [Discord RPC reference — SET_ACTIVITY](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx)

The current implementation uses Discord's local discord-ipc-{n} named pipe, sends SET_ACTIVITY, and waits for the response nonce. The application's UI must report Discord as active only after the server acknowledges an activity update.

## Field limits and lyric integrity

Discord text fields are limited. SkyLyrics normalizes whitespace and clips the current lyric state to the supported field length. A long lyric line is shortened rather than split into multiple lines. The track title remains in Details.

The line is taken from the active line in the loaded timestamped lyrics document. If the source is unsynchronized, unavailable, or the playback position is before the first timestamp, SkyLyrics sends the normal track/artist state rather than pretending an unsynced line is current.
