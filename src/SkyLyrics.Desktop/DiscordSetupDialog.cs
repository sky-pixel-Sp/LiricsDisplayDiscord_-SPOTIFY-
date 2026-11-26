using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SkyLyrics.Desktop;

public sealed class DiscordSetupDialog : Window
{
    private readonly TextBox _applicationIdInput;

    public string ApplicationId => _applicationIdInput.Text.Trim();

    public DiscordSetupDialog(string? currentApplicationId)
    {
        Title = "Connect SkyLyrics to Discord";
        Width = 540;
        Height = 390;
        MinWidth = 540;
        MinHeight = 390;
        MaxWidth = 540;
        MaxHeight = 390;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush");
        Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush");
        FontFamily = new FontFamily("Segoe UI Variable");

        var root = new StackPanel { Margin = new Thickness(26) };

        root.Children.Add(new TextBlock
        {
            Text = "Discord Rich Presence",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush")
        });

        root.Children.Add(new TextBlock
        {
            Text = "Register SkyLyrics as a Discord application, then paste its public Application ID below.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 8, 0, 18)
        });

        root.Children.Add(new TextBlock
        {
            Text = "APPLICATION ID",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 7)
        });

        _applicationIdInput = new TextBox
        {
            Text = currentApplicationId ?? string.Empty,
            FontSize = 14,
            Padding = new Thickness(12, 9, 12, 9),
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("StrokeBrush"),
            BorderThickness = new Thickness(1),
            ToolTip = "Numeric Application ID from Discord Developer Portal"
        };
        root.Children.Add(_applicationIdInput);

        root.Children.Add(new TextBlock
        {
            Text = "No bot token, Discord password, or OAuth login is required. Discord Desktop must be running when you connect.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 12, 0, 18)
        });

        var footer = new DockPanel();

        var portalButton = new Button
        {
            Content = "Open Developer Portal",
            Padding = new Thickness(13, 9, 13, 9),
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)Application.Current.FindResource("QuietButtonStyle")
        };
        portalButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://discord.com/developers/applications")
            {
                UseShellExecute = true
            });
        DockPanel.SetDock(portalButton, Dock.Left);
        footer.Children.Add(portalButton);

        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(15, 9, 15, 9),
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)Application.Current.FindResource("QuietButtonStyle"),
            IsCancel = true
        };
        actionButtons.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save Application ID",
            Padding = new Thickness(15, 9, 15, 9),
            Style = (Style)Application.Current.FindResource("AccentButtonStyle"),
            IsDefault = true
        };
        saveButton.Click += SaveButton_Click;
        actionButtons.Children.Add(saveButton);

        DockPanel.SetDock(actionButtons, Dock.Right);
        footer.Children.Add(actionButtons);
        root.Children.Add(footer);

        Content = root;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DiscordApplicationIdStore.IsValidApplicationId(ApplicationId))
        {
            MessageBox.Show(
                this,
                "The Application ID must be a numeric ID copied from Discord Developer Portal.",
                "Invalid Application ID",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
