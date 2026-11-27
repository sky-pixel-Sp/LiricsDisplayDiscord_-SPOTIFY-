namespace SkyLyrics.Application.Settings;

public sealed record AppPreferences
{
    public string Theme { get; init; } = "Dark";
    public int SyncOffsetMilliseconds { get; init; }
    public bool ReduceMotion { get; init; }
    public bool MiniPlayerAlwaysOnTop { get; init; } = true;
    public bool DiscordPresenceEnabled { get; init; }
    public string DiscordApplicationId { get; init; } = string.Empty;
    public bool ObsOverlayEnabled { get; init; }
    public GameModePreferences GameMode { get; init; } = new();
    public int ObsOverlayPort { get; init; } = 43821;

    public AppPreferences Normalize()
    {
        var id = (DiscordApplicationId ?? string.Empty).Trim();
        return this with
        {
            Theme = string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
            SyncOffsetMilliseconds = Math.Clamp(SyncOffsetMilliseconds, -500, 500),
            DiscordApplicationId = id,
            ObsOverlayPort = Math.Clamp(ObsOverlayPort, 1024, 65535),
            GameMode = (GameMode ?? new GameModePreferences()).Normalize(),
            DiscordPresenceEnabled = DiscordPresenceEnabled && IsValidDiscordApplicationId(id)
        };
    }

    public static bool IsValidDiscordApplicationId(string? value) =>
        value is { Length: >= 17 and <= 22 } &&
        value.All(static character => character is >= '0' and <= '9');
}

public interface IAppPreferencesStore
{
    AppPreferences Current { get; }
    event Action<AppPreferences>? PreferencesChanged;
    ValueTask SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default);
}
