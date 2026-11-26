# Discord Rich Presence — integration status

## Target behavior

SkyLyrics should publish the current Spotify Desktop track to the user's Discord profile:

- Activity type: Listening.
- Details: current Spotify track title.
- State: artist, with a Paused indicator when playback is paused.
- Timestamps: based only on Windows GSMTC playback position and duration.
- Clear the presence when Spotify has no active track, when the user disconnects, or when SkyLyrics exits.

No lyrics, Discord messages, voice-channel data, audio capture, or playback controls are part of this integration.

## Discord setup requirements

A Discord application must be registered in the [Discord Developer Portal](https://discord.com/developers/applications). Copy the public Application ID from the application's General Information page. Discord Desktop must be running and activity sharing must be enabled.

## Important implementation compatibility note

Discord documents two related but distinct integration paths:

1. The general RPC-over-IPC documentation describes the `discord-ipc-{n}` named pipe and the `SET_ACTIVITY` command. It also states that RPC commands require an authenticated client.
2. The newer Discord Social SDK documents a desktop Rich Presence path that does not require OAuth authentication. It requires a registered Application ID, a running Discord desktop client, and the official native SDK.

The current Phase 5 draft contains a minimal raw IPC prototype. Its Windows build and unit tests pass, but those checks do not prove that Discord will accept an unauthenticated `SET_ACTIVITY` command on a live desktop client. **Do not treat this draft as release-ready or merge it until the live protocol path is validated and moved to the supported Social SDK flow if needed.**

The Social SDK is distributed separately through the authenticated Discord Developer Portal. Its native runtime must be bundled with the application in accordance with Discord's SDK terms. It must not be copied into this public source repository as a loose SDK distribution.

## Privacy

The intended direct Rich Presence path does not require a bot account, bot token, Discord password, or user OAuth login. Only the public Application ID is saved locally. If a different authenticated RPC path is chosen, that would require an explicit product decision and user consent; it must not be enabled silently.
