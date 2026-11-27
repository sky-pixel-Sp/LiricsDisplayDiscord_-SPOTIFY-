using SkyLyrics.Application.Customization;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class CustomizationPreferencesTests
{
    [Fact]
    public void Normalize_ProvidesThreeRealBuiltInProfiles()
    {
        var preferences = new CustomizationPreferences
        {
            Profiles = []
        }.Normalize();

        Assert.Equal(3, preferences.Profiles.Count);
        Assert.Contains(preferences.Profiles, profile => profile.Id == "sage");
        Assert.Contains(preferences.Profiles, profile => profile.Id == "midnight");
        Assert.Contains(preferences.Profiles, profile => profile.Id == "paper");
        Assert.Equal("sage", preferences.ActiveProfileId);
    }

    [Fact]
    public void Normalize_RejectsDuplicateAndInvalidIdsAndCapsProfileCount()
    {
        var profiles = Enumerable.Range(0, 30)
            .Select(index => new ThemeProfile
            {
                Id = index == 0 ? "my-theme" : index == 1 ? "my-theme" : $"custom-{index}",
                Name = $"Theme {index}"
            })
            .ToList();
        profiles.Add(new ThemeProfile { Id = "bad id" });

        var normalized = new CustomizationPreferences
        {
            Profiles = profiles,
            ActiveProfileId = "missing"
        }.Normalize();

        Assert.True(normalized.Profiles.Count <= CustomizationPreferences.MaximumProfiles);
        Assert.Equal(1, normalized.Profiles.Count(profile => profile.Id == "my-theme"));
        Assert.DoesNotContain(normalized.Profiles, profile => profile.Id == "bad id");
        Assert.Equal("sage", normalized.ActiveProfileId);
    }

    [Fact]
    public void ThemeProfile_NormalizeBoundsAndSanitizesUserValues()
    {
        var profile = new ThemeProfile
        {
            Id = "my-theme",
            Name = new string('x', 80),
            AccentColor = "url(javascript:alert(1))",
            BackgroundColor = "#123abc",
            FontFamily = "  ",
            FontScale = double.NaN,
            LyricAlignment = "Unsupported",
            ArtworkTreatment = "RemoteImage",
            AnimationPreset = "RunCode"
        }.Normalize();

        Assert.Equal(40, profile.Name.Length);
        Assert.Equal("#83E1D2", profile.AccentColor);
        Assert.Equal("#123ABC", profile.BackgroundColor);
        Assert.Equal("Segoe UI", profile.FontFamily);
        Assert.Equal(1d, profile.FontScale);
        Assert.Equal("Center", profile.LyricAlignment);
        Assert.Equal("Ambient", profile.ArtworkTreatment);
        Assert.Equal("Cinematic", profile.AnimationPreset);
    }

    [Theory]
    [InlineData("#000000", true)]
    [InlineData("#aBc123", true)]
    [InlineData("#12345", false)]
    [InlineData("1234567", false)]
    [InlineData("#12GG00", false)]
    [InlineData("url(#fff)", false)]
    public void IsValidColor_AcceptsOnlyHexRgb(string value, bool expected)
    {
        Assert.Equal(expected, ThemeProfile.IsValidColor(value));
    }
}
