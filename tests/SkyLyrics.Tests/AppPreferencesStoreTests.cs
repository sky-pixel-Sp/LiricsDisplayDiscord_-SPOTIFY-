using Microsoft.Extensions.Logging.Abstractions;
using SkyLyrics.Application.Customization;
using SkyLyrics.Application.Settings;
using SkyLyrics.Infrastructure.Persistence;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class AppPreferencesStoreTests
{
    [Fact]
    public void Normalize_ClampsOffsetAndPortAndDisablesInvalidDiscordApplicationId()
    {
        var preferences = new AppPreferences
        {
            Theme = "unknown",
            SyncOffsetMilliseconds = 900,
            ObsOverlayPort = 80,
            DiscordPresenceEnabled = true,
            DiscordApplicationId = "not-an-application-id"
        }.Normalize();

        Assert.Equal("Dark", preferences.Theme);
        Assert.Equal(500, preferences.SyncOffsetMilliseconds);
        Assert.Equal(1024, preferences.ObsOverlayPort);
        Assert.False(preferences.DiscordPresenceEnabled);
        Assert.Equal("not-an-application-id", preferences.DiscordApplicationId);
    }

    [Fact]
    public async Task PreferencesStore_PersistsUserSelectedSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyLyricsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        var store = new AppPreferencesStore(NullLogger<AppPreferencesStore>.Instance, path);
        var restored = new AppPreferencesStore(NullLogger<AppPreferencesStore>.Instance, path);

        try
        {
            await store.SaveAsync(new AppPreferences
            {
                Theme = "Light",
                SyncOffsetMilliseconds = -125,
                SelectedMediaSessionSourceAppUserModelId = "chrome.exe",
                ReduceMotion = true,
                SplashEnabled = false,
                LyricAnimationLeadMilliseconds = 110,
                CinematicQualityTier = "cinematic",
                ObsOverlayEnabled = true,
                ObsOverlayPort = 43822,
                Customization = new CustomizationPreferences
                {
                    ActiveProfileId = "my-night",
                    Profiles =
                    [
                        CustomizationPreferences.Sage,
                        CustomizationPreferences.Midnight,
                        CustomizationPreferences.Paper,
                        new ThemeProfile
                        {
                            Id = "my-night",
                            Name = "My Night",
                            AccentColor = "#AABBCC",
                            FontScale = 1.15d,
                            AnimationPreset = "Subtle"
                        }
                    ]
                }
            });

            await restored.StartAsync(CancellationToken.None);

            Assert.Equal("Light", restored.Current.Theme);
            Assert.Equal(-125, restored.Current.SyncOffsetMilliseconds);
            Assert.Equal("chrome.exe", restored.Current.SelectedMediaSessionSourceAppUserModelId);
            Assert.True(restored.Current.ReduceMotion);
            Assert.False(restored.Current.SplashEnabled);
            Assert.Equal(110, restored.Current.LyricAnimationLeadMilliseconds);
            Assert.Equal("cinematic", restored.Current.CinematicQualityTier);
            Assert.True(restored.Current.ObsOverlayEnabled);
            Assert.Equal(43822, restored.Current.ObsOverlayPort);
            Assert.Equal("my-night", restored.Current.Customization.ActiveProfileId);
            var active = Assert.Single(restored.Current.Customization.Profiles, profile => profile.Id == "my-night");
            Assert.Equal("#AABBCC", active.AccentColor);
            Assert.Equal(1.15d, active.FontScale);
        }
        finally
        {
            store.Dispose();
            restored.Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Normalize_ClampsLyricAnimationLeadAndValidatesCinematicQuality()
    {
        var preferences = new AppPreferences
        {
            LyricAnimationLeadMilliseconds = 240,
            CinematicQualityTier = "unknown"
        }.Normalize();

        Assert.Equal(120, preferences.LyricAnimationLeadMilliseconds);
        Assert.Equal("balanced", preferences.CinematicQualityTier);
    }

    [Theory]
    [InlineData("  Spotify.exe  ", "Spotify.exe")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_CleansSelectedMediaSource(string? input, string? expected)
    {
        var preferences = new AppPreferences
        {
            SelectedMediaSessionSourceAppUserModelId = input
        }.Normalize();

        Assert.Equal(expected, preferences.SelectedMediaSessionSourceAppUserModelId);
    }

    [Fact]
    public void Normalize_DeduplicatesLocalTranslationsByTrackKeyAndKeepsLatest()
    {
        var path = Path.Combine(Path.GetTempPath(), "translation.lrc");
        var key = new string('d', 64);
        var preferences = new AppPreferences
        {
            LocalTranslations =
            [
                new LocalTranslationPreference(key, path, "Japanese"),
                new LocalTranslationPreference(key.ToUpperInvariant(), path, "Indonesian")
            ]
        }.Normalize();

        var profile = Assert.Single(preferences.LocalTranslations);
        Assert.Equal(key, profile.TrackKey);
        Assert.Equal("Indonesian", profile.Language);
    }

    [Fact]
    public void Normalize_ClampsVisualSettingsToSupportedModesAndBudgets()
    {
        var preferences = new AppPreferences
        {
            Visuals = new VisualStudioPreferences
            {
                ModeId = "unknown",
                QualityId = "unknown",
                Intensity = 140
            }
        }.Normalize();

        Assert.Equal("album-ambient", preferences.Visuals.ModeId);
        Assert.Equal("balanced", preferences.Visuals.QualityId);
        Assert.Equal(100, preferences.Visuals.Intensity);
        Assert.Equal(20, preferences.Visuals.ParticleBudget);
        Assert.Equal(24, preferences.Visuals.FrameRateCap);
    }

    [Theory]
    [InlineData("eco", 8, 12)]
    [InlineData("balanced", 20, 24)]
    [InlineData("high", 36, 30)]
    [InlineData("ultra", 56, 45)]
    public void VisualQualityMapsToBoundedResourceBudget(string quality, int particles, int frameRate)
    {
        var preferences = new VisualStudioPreferences { QualityId = quality }.Normalize();

        Assert.Equal(particles, preferences.ParticleBudget);
        Assert.Equal(frameRate, preferences.FrameRateCap);
    }

    [Fact]
    public void DiscordApplicationId_RequiresOnlyNumericSnowflakeCharacters()
    {
        Assert.True(AppPreferences.IsValidDiscordApplicationId("12345678901234567"));
        Assert.True(AppPreferences.IsValidDiscordApplicationId("1234567890123456789012"));
        Assert.False(AppPreferences.IsValidDiscordApplicationId("1234"));
        Assert.False(AppPreferences.IsValidDiscordApplicationId("1234567890123456x"));
    }
}
