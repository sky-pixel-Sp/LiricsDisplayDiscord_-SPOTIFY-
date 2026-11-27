using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Desktop.Services;

namespace SkyLyrics.Desktop;

public partial class SettingsWindow : Window
{
    private readonly IAppPreferencesStore _preferences;
    private readonly SyncScheduler _scheduler;

    public SettingsWindow(IAppPreferencesStore preferences, SyncScheduler scheduler)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        InitializeComponent();
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        var current = _preferences.Current;
        ThemeCombo.SelectedIndex = string.Equals(current.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ThemeProfileCombo.ItemsSource = current.Customization.Profiles;
        ThemeProfileCombo.SelectedValue = current.Customization.ActiveProfileId;
        ReduceMotionCheck.IsChecked = current.ReduceMotion;
        MiniPlayerTopmostCheck.IsChecked = current.MiniPlayerAlwaysOnTop;
        OffsetBox.Text = current.SyncOffsetMilliseconds.ToString(CultureInfo.InvariantCulture);
        DiscordEnabledCheck.IsChecked = current.DiscordPresenceEnabled;
        DiscordApplicationIdBox.Text = current.DiscordApplicationId;
        ObsEnabledCheck.IsChecked = current.ObsOverlayEnabled;
        ObsPortBox.Text = current.ObsOverlayPort.ToString(CultureInfo.InvariantCulture);
    }

    private async void ConfigureGameMode_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GameModeSettingsWindow(_preferences.Current.GameMode) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } gameMode) return;

        try
        {
            await _preferences.SaveAsync(_preferences.Current with { GameMode = gameMode });
            StatusText.Text = "Game Mode settings saved and applied.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Game Mode settings could not be saved: {exception.Message}";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(OffsetBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) ||
            offset is < -500 or > 500)
        {
            StatusText.Text = "Playback offset must be a whole number between -500 and 500.";
            return;
        }

        if (!int.TryParse(ObsPortBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
            port is < 1024 or > 65535)
        {
            StatusText.Text = "OBS overlay port must be between 1024 and 65535.";
            return;
        }

        var discordId = DiscordApplicationIdBox.Text.Trim();
        if (DiscordEnabledCheck.IsChecked == true && !AppPreferences.IsValidDiscordApplicationId(discordId))
        {
            StatusText.Text = "Enter the numeric Application ID from your Discord Developer Portal, or disable Rich Presence.";
            return;
        }

        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Dark";
        var customization = _preferences.Current.Customization with
        {
            ActiveProfileId = ThemeProfileCombo.SelectedValue as string ?? "sage"
        };
        var updated = _preferences.Current with
        {
            Theme = theme,
            Customization = customization,
            ReduceMotion = ReduceMotionCheck.IsChecked == true,
            MiniPlayerAlwaysOnTop = MiniPlayerTopmostCheck.IsChecked == true,
            SyncOffsetMilliseconds = offset,
            DiscordPresenceEnabled = DiscordEnabledCheck.IsChecked == true,
            DiscordApplicationId = discordId,
            ObsOverlayEnabled = ObsEnabledCheck.IsChecked == true,
            ObsOverlayPort = port
        };

        try
        {
            await _preferences.SaveAsync(updated);
            _scheduler.SetOffset(TimeSpan.FromMilliseconds(offset));
            ThemeManager.Apply(theme);
            var activeProfile = updated.Customization.Normalize().Profiles
                .First(profile => string.Equals(profile.Id, customization.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
            ThemeManager.ApplyProfile(activeProfile);
            StatusText.Text = "Settings saved. Output configuration is refreshed immediately.";
            DialogResult = true;
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Settings could not be saved: {exception.Message}";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
