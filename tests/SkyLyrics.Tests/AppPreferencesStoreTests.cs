using Microsoft.Extensions.Logging.Abstractions;
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
                ReduceMotion = true,
                ObsOverlayEnabled = true,
                ObsOverlayPort = 43822
            });

            await restored.StartAsync(CancellationToken.None);

            Assert.Equal("Light", restored.Current.Theme);
            Assert.Equal(-125, restored.Current.SyncOffsetMilliseconds);
            Assert.True(restored.Current.ReduceMotion);
            Assert.True(restored.Current.ObsOverlayEnabled);
            Assert.Equal(43822, restored.Current.ObsOverlayPort);
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
    public void DiscordApplicationId_RequiresOnlyNumericSnowflakeCharacters()
    {
        Assert.True(AppPreferences.IsValidDiscordApplicationId("12345678901234567"));
        Assert.True(AppPreferences.IsValidDiscordApplicationId("1234567890123456789012"));
        Assert.False(AppPreferences.IsValidDiscordApplicationId("1234"));
        Assert.False(AppPreferences.IsValidDiscordApplicationId("1234567890123456x"));
    }
}
