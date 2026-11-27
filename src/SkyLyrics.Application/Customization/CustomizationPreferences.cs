namespace SkyLyrics.Application.Customization;

public sealed record CustomizationPreferences
{
    public const int MaximumProfiles = 16;

    public string ActiveProfileId { get; init; } = "sage";
    public List<ThemeProfile> Profiles { get; init; } = CreateBuiltIns();

    public static ThemeProfile Sage { get; } = new()
    {
        Id = "sage",
        Name = "Sage",
        AccentColor = "#83E1D2",
        BackgroundColor = "#111817",
        SurfaceColor = "#1A2421",
        PrimaryTextColor = "#F2F6F3",
        SecondaryTextColor = "#AAB8B1",
        FontFamily = "Segoe UI",
        FontScale = 1d,
        LyricAlignment = "Center",
        ArtworkTreatment = "Ambient",
        AnimationPreset = "Cinematic"
    };

    public static ThemeProfile Midnight { get; } = new()
    {
        Id = "midnight",
        Name = "Midnight",
        AccentColor = "#9DAEFF",
        BackgroundColor = "#101321",
        SurfaceColor = "#1A2033",
        PrimaryTextColor = "#F3F4FC",
        SecondaryTextColor = "#A9B0C9",
        FontFamily = "Segoe UI",
        FontScale = 1d,
        LyricAlignment = "Center",
        ArtworkTreatment = "Cover",
        AnimationPreset = "Subtle"
    };

    public static ThemeProfile Paper { get; } = new()
    {
        Id = "paper",
        Name = "Paper",
        AccentColor = "#486C62",
        BackgroundColor = "#F3F1EA",
        SurfaceColor = "#FFFFFF",
        PrimaryTextColor = "#202925",
        SecondaryTextColor = "#68736D",
        FontFamily = "Segoe UI",
        FontScale = 1d,
        LyricAlignment = "Left",
        ArtworkTreatment = "Minimal",
        AnimationPreset = "Static"
    };

    public CustomizationPreferences Normalize()
    {
        var normalized = new List<ThemeProfile>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in Profiles ?? [])
        {
            if (profile is null || !ThemeProfile.IsValidId(profile.Id) || !ids.Add(profile.Id))
            {
                continue;
            }

            normalized.Add(profile.Normalize());
            if (normalized.Count >= MaximumProfiles)
            {
                break;
            }
        }

        foreach (var builtIn in new[] { Sage, Midnight, Paper })
        {
            if (ids.Add(builtIn.Id))
            {
                normalized.Insert(0, builtIn);
            }
        }

        if (normalized.Count > MaximumProfiles)
        {
            normalized.RemoveRange(MaximumProfiles, normalized.Count - MaximumProfiles);
        }

        var active = normalized.Any(profile =>
            string.Equals(profile.Id, ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            ? ActiveProfileId
            : Sage.Id;

        return this with
        {
            ActiveProfileId = active,
            Profiles = normalized
        };
    }

    private static List<ThemeProfile> CreateBuiltIns() => [Sage, Midnight, Paper];
}
