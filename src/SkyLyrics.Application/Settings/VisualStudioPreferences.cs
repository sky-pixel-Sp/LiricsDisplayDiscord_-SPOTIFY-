namespace SkyLyrics.Application.Settings;

public sealed record VisualStudioPreferences
{
    public string ModeId { get; init; } = "album-ambient";
    public string QualityId { get; init; } = "balanced";
    public int Intensity { get; init; } = 72;

    public VisualStudioPreferences Normalize()
    {
        var mode = ModeId is "album-ambient" or "fluid-gradient" or "particle-field"
            ? ModeId
            : "album-ambient";
        var quality = QualityId is "eco" or "balanced" or "high" or "ultra"
            ? QualityId
            : "balanced";

        return this with
        {
            ModeId = mode,
            QualityId = quality,
            Intensity = Math.Clamp(Intensity, 0, 100)
        };
    }

    public int ParticleBudget => Normalize().QualityId switch
    {
        "eco" => 8,
        "high" => 36,
        "ultra" => 56,
        _ => 20
    };

    public int FrameRateCap => Normalize().QualityId switch
    {
        "eco" => 12,
        "high" => 30,
        "ultra" => 45,
        _ => 24
    };
}
