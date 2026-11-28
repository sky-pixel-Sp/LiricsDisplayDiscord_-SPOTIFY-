using System.Collections.Immutable;
using SkyLyrics.Application.Customization;

namespace SkyLyrics.Application.Settings;

public sealed record LocalTranslationPreference(
    string TrackKey,
    string FilePath,
    string Language);

public sealed record AppPreferences
{
    public string Theme { get; init; } = "Dark";
    public int SyncOffsetMilliseconds { get; init; }
    public string? SelectedMediaSessionSourceAppUserModelId { get; init; }
    public bool ReduceMotion { get; init; }
    public bool SplashEnabled { get; init; } = true;
    public int LyricAnimationLeadMilliseconds { get; init; } = 90;
    public string CinematicQualityTier { get; init; } = "balanced";
    public bool LetterboxEnabled { get; init; }
    public bool MiniPlayerAlwaysOnTop { get; init; } = true;
    public bool DiscordPresenceEnabled { get; init; }
    public string DiscordApplicationId { get; init; } = string.Empty;
    public bool ObsOverlayEnabled { get; init; }
    public GameModePreferences GameMode { get; init; } = new();
    public WindowGeometryPreference? MainWindowGeometry { get; init; }
    public VisualStudioPreferences Visuals { get; init; } = new();
    public ImmutableArray<LocalTranslationPreference> LocalTranslations { get; init; } =
        ImmutableArray<LocalTranslationPreference>.Empty;
    public int ObsOverlayPort { get; init; } = 43821;
    public CustomizationPreferences Customization { get; init; } = new();
    public AssetLibraryPreferences AssetLibrary { get; init; } = new();

    public AppPreferences Normalize()
    {
        var id = (DiscordApplicationId ?? string.Empty).Trim();
        return this with
        {
            Theme = string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
            SyncOffsetMilliseconds = Math.Clamp(SyncOffsetMilliseconds, -500, 500),
            LyricAnimationLeadMilliseconds = Math.Clamp(LyricAnimationLeadMilliseconds, 60, 120),
            CinematicQualityTier = CinematicQualityTier is "cinematic" or "balanced" or "lite" or "reduced"
                ? CinematicQualityTier
                : "balanced",
            SelectedMediaSessionSourceAppUserModelId = string.IsNullOrWhiteSpace(SelectedMediaSessionSourceAppUserModelId)
                ? null
                : SelectedMediaSessionSourceAppUserModelId.Trim(),
            DiscordApplicationId = id,
            ObsOverlayPort = Math.Clamp(ObsOverlayPort, 1024, 65535),
            GameMode = (GameMode ?? new GameModePreferences()).Normalize(),
            MainWindowGeometry = MainWindowGeometry?.Normalize(),
            Visuals = (Visuals ?? new VisualStudioPreferences()).Normalize(),
            LocalTranslations = NormalizeLocalTranslations(LocalTranslations),
            Customization = (Customization ?? new CustomizationPreferences()).Normalize(),
            AssetLibrary = (AssetLibrary ?? new AssetLibraryPreferences()).Normalize(),
            DiscordPresenceEnabled = DiscordPresenceEnabled && IsValidDiscordApplicationId(id)
        };
    }

    private static ImmutableArray<LocalTranslationPreference> NormalizeLocalTranslations(
        ImmutableArray<LocalTranslationPreference> profiles)
    {
        if (profiles.IsDefaultOrEmpty)
        {
            return ImmutableArray<LocalTranslationPreference>.Empty;
        }

        return profiles
            .Where(profile =>
                profile is not null &&
                profile.TrackKey is { Length: 64 } &&
                profile.TrackKey.All(Uri.IsHexDigit) &&
                !string.IsNullOrWhiteSpace(profile.FilePath) &&
                Path.IsPathFullyQualified(profile.FilePath) &&
                !string.IsNullOrWhiteSpace(profile.Language))
            .Select(profile => profile with
            {
                TrackKey = profile.TrackKey.ToLowerInvariant(),
                FilePath = profile.FilePath.Trim(),
                Language = profile.Language.Trim().Length > 64
                    ? profile.Language.Trim()[..64]
                    : profile.Language.Trim()
            })
            .GroupBy(profile => profile.TrackKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .TakeLast(256)
            .ToImmutableArray();
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
