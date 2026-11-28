using System.Collections.Immutable;

namespace SkyLyrics.Application.Settings;

public sealed record AssetLibraryPreferences
{
    public string SelectedBackgroundId { get; init; } = string.Empty;
    public string SelectedAnimationId { get; init; } = string.Empty;
    public string SelectedVisualizerStyleId { get; init; } = "spectrum";
    public string QualityProfile { get; init; } = "balanced";
    public ImmutableArray<string> FavoriteAssetIds { get; init; } = ImmutableArray<string>.Empty;

    public AssetLibraryPreferences Normalize()
    {
        static string NormalizeId(string? value, string fallback = "") =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

        var favorites = FavoriteAssetIds.IsDefaultOrEmpty
            ? ImmutableArray<string>.Empty
            : FavoriteAssetIds
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Take(512)
                .ToImmutableArray();

        var quality = QualityProfile?.Trim().ToLowerInvariant();
        return this with
        {
            SelectedBackgroundId = NormalizeId(SelectedBackgroundId),
            SelectedAnimationId = NormalizeId(SelectedAnimationId),
            SelectedVisualizerStyleId = NormalizeId(SelectedVisualizerStyleId, "spectrum"),
            QualityProfile = quality is "low" or "balanced" or "high" ? quality : "balanced",
            FavoriteAssetIds = favorites
        };
    }
}
