using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SkyLyrics.Application.Settings;
using SkyLyrics.Application.Playback;
using SkyLyrics.Application.Synchronization;
using SkyLyrics.Desktop.Services;

namespace SkyLyrics.Desktop;

public partial class SettingsWindow : Window
{
    private readonly IAppPreferencesStore _preferences;
    private readonly SyncScheduler _scheduler;
    private readonly ISelectablePlaybackSessionSource _playbackSource;

    private sealed record MediaSourceOption(string SourceAppUserModelId, string DisplayName);

    public SettingsWindow(
        IAppPreferencesStore preferences,
        SyncScheduler scheduler,
        ISelectablePlaybackSessionSource playbackSource)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _playbackSource = playbackSource ?? throw new ArgumentNullException(nameof(playbackSource));
        InitializeComponent();
        LoadPreferences();
        _playbackSource.AvailableSessionsChanged += OnAvailableSessionsChanged;
        Closed += (_, _) => _playbackSource.AvailableSessionsChanged -= OnAvailableSessionsChanged;
    }

    private void OnAvailableSessionsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            Dispatcher.BeginInvoke(new Action(RefreshMediaSources));
        }
    }

    private void RefreshMediaSources()
    {
        var selected = _preferences.Current.SelectedMediaSessionSourceAppUserModelId;
        var options = new List<MediaSourceOption>
        {
            new(string.Empty, "Automatic · follow active playing session")
        };

        options.AddRange(_playbackSource.AvailableSessions.Select(session =>
            new MediaSourceOption(
                session.SourceAppUserModelId,
                $"{session.DisplayName}{(session.IsPlaying ? " · PLAYING" : string.Empty)}")));

        if (!string.IsNullOrWhiteSpace(selected) &&
            options.All(option => !StringComparer.OrdinalIgnoreCase.Equals(option.SourceAppUserModelId, selected)))
        {
            options.Add(new MediaSourceOption(selected, $"{selected} · SAVED SOURCE NOT CURRENTLY AVAILABLE"));
        }

        MediaSourceCombo.ItemsSource = options;
        MediaSourceCombo.SelectedValue = selected ?? string.Empty;
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
        RefreshMediaSources();
    }

    private void OpenCustomizationStudio_Click(object sender, RoutedEventArgs e)
    {
        var studio = new CustomizationStudioWindow(_preferences) { Owner = this };
        if (studio.ShowDialog() == true)
        {
            LoadPreferences();
            StatusText.Text = "Customization profile applied.";
        }
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
        var selectedSource = MediaSourceCombo.SelectedValue as string;
        if (!string.IsNullOrWhiteSpace(selectedSource) &&
            !_playbackSource.AvailableSessions.Any(session =>
                StringComparer.OrdinalIgnoreCase.Equals(session.SourceAppUserModelId, selectedSource)))
        {
            StatusText.Text = "The selected media source is no longer available. Choose Automatic or refresh the available sessions.";
            RefreshMediaSources();
            return;
        }

        var updated = _preferences.Current with
        {
            SelectedMediaSessionSourceAppUserModelId = string.IsNullOrWhiteSpace(selectedSource) ? null : selectedSource,
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
