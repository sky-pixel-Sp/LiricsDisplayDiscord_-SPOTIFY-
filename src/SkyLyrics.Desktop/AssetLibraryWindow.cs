using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkyLyrics.Application.Settings;
using SkyLyrics.Infrastructure.Assets;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using Image = System.Windows.Controls.Image;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace SkyLyrics.Desktop;

public sealed class AssetLibraryWindow : Window
{
    private readonly IAppPreferencesStore _preferences;
    private readonly AssetCatalogService _catalog;
    private readonly ComboBox _category = new();
    private readonly ComboBox _quality = new();
    private readonly TextBox _search = new();
    private readonly ListBox _assetList = new();
    private readonly Image _previewImage = new();
    private readonly MediaElement _previewVideo = new();
    private readonly TextBlock _details = new();
    private readonly TextBlock _status = new();
    private readonly Button _favoriteButton;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _resetButton;
    private readonly DispatcherTimer _gifTimer;
    private IReadOnlyList<BitmapFrame> _gifFrames = [];
    private AssetCatalogEntry? _selectedAsset;
    private int _gifFrameIndex;
    private bool _previewRequested;
    private bool _loading = true;

    public AssetLibraryWindow(IAppPreferencesStore preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _catalog = new AssetCatalogService(Path.Combine(AppContext.BaseDirectory, "Assets", "Library"));

        Title = "SkyLyrics · Asset Library";
        Width = 1180;
        Height = 780;
        MinWidth = 900;
        MinHeight = 600;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0D10");
        Foreground = Brush("#F4F5F2");
        FontFamily = System.Windows.Application.Current?.Resources["PoppinsFontFamily"] as FontFamily
            ?? new FontFamily("Segoe UI Variable Display");

        _favoriteButton = CreateButton("ADD TO FAVORITES");
        _favoriteButton.Click += Favorite_Click;
        _previewButton = CreateButton("PREVIEW");
        _previewButton.Click += Preview_Click;
        _applyButton = CreateButton("APPLY");
        _applyButton.Click += Apply_Click;
        _resetButton = CreateButton("RESET");
        _resetButton.Click += Reset_Click;

        _gifTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _gifTimer.Tick += GifTimer_Tick;

        BuildInterface();
        Loaded += async (_, _) => await LoadAssetsAsync();
        Activated += (_, _) => ResumePreview();
        Deactivated += (_, _) => PausePreview();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) ResumePreview();
            else PausePreview();
        };
        Closed += (_, _) =>
        {
            _gifTimer.Stop();
            _previewVideo.Stop();
        };
    }

    private void BuildInterface()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(312) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "CREATIVE LIBRARY",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Set the atmosphere.",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Browse installed media files. Previews always load from the selected asset.",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        header.Children.Add(heading);
        var close = CreateButton("CLOSE");
        close.VerticalAlignment = VerticalAlignment.Center;
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        Grid.SetColumnSpan(header, 2);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var sidebar = new Border
        {
            Background = Brush("#12161B"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 14, 0)
        };
        var sideLayout = new Grid();
        sideLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sideLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sideLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sideLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        sideLayout.Children.Add(Eyebrow("BROWSE ASSETS"));
        _category.Items.Add("All assets");
        _category.Items.Add("Backgrounds");
        _category.Items.Add("Animations");
        _category.Items.Add("Visualizers");
        _category.Items.Add("Textures");
        _category.Items.Add("Artwork");
        _category.Items.Add("Favorites");
        _category.SelectedIndex = 0;
        _category.MinHeight = 34;
        _category.Margin = new Thickness(0, 10, 0, 10);
        _category.SelectionChanged += (_, _) => RebuildAssetList();
        Grid.SetRow(_category, 1);
        sideLayout.Children.Add(_category);

        _search.Height = 36;
        _search.Padding = new Thickness(10, 7, 10, 7);
        _search.Background = Brush("#0C1014");
        _search.Foreground = Foreground;
        _search.BorderBrush = Brush("#28323A");
        _search.ToolTip = "Search by name, category, tag, or source";
        _search.Margin = new Thickness(0, 0, 0, 12);
        _search.TextChanged += (_, _) => RebuildAssetList();
        Grid.SetRow(_search, 2);
        sideLayout.Children.Add(_search);

        _assetList.BorderThickness = new Thickness(0);
        _assetList.Background = Brushes.Transparent;
        _assetList.Foreground = Foreground;
        _assetList.SelectionChanged += AssetList_SelectionChanged;
        Grid.SetRow(_assetList, 3);
        sideLayout.Children.Add(_assetList);
        sidebar.Child = sideLayout;
        Grid.SetRow(sidebar, 1);
        root.Children.Add(sidebar);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var previewFrame = new Border
        {
            Background = Brush("#080A0D"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(8),
            ClipToBounds = true
        };
        var previewLayer = new Grid();
        _previewImage.Stretch = Stretch.Uniform;
        _previewImage.HorizontalAlignment = HorizontalAlignment.Stretch;
        _previewImage.VerticalAlignment = VerticalAlignment.Stretch;
        _previewImage.SnapsToDevicePixels = true;
        _previewVideo.LoadedBehavior = MediaState.Manual;
        _previewVideo.UnloadedBehavior = MediaState.Manual;
        _previewVideo.Stretch = Stretch.Uniform;
        _previewVideo.Visibility = Visibility.Collapsed;
        _previewVideo.MediaEnded += (_, _) =>
        {
            if (_previewRequested && IsVisible && !_preferences.Current.ReduceMotion)
            {
                _previewVideo.Position = TimeSpan.Zero;
                _previewVideo.Play();
            }
        };
        _previewVideo.MediaFailed += (_, args) =>
        {
            _status.Text = $"Video preview unavailable: {args.ErrorException?.Message ?? "media decoder error"}";
            _previewRequested = false;
        };
        previewLayer.Children.Add(_previewImage);
        previewLayer.Children.Add(_previewVideo);
        previewFrame.Child = previewLayer;
        Grid.SetRow(previewFrame, 0);
        content.Children.Add(previewFrame);

        var detailsPanel = new Border
        {
            Background = Brush("#12161B"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 12, 0, 0)
        };
        _details.Text = "Select a real media file to inspect its dimensions, format, source, license and checksum.";
        _details.TextWrapping = TextWrapping.Wrap;
        _details.FontSize = 10;
        _details.Foreground = Brush("#CDD5D0");
        detailsPanel.Child = _details;
        Grid.SetRow(detailsPanel, 1);
        content.Children.Add(detailsPanel);

        var actionRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _quality.Items.Add("Low quality");
        _quality.Items.Add("Balanced quality");
        _quality.Items.Add("High quality");
        _quality.MinWidth = 138;
        _quality.SelectedIndex = _preferences.Current.AssetLibrary.QualityProfile switch
        {
            "low" => 0,
            "high" => 2,
            _ => 1
        };
        _quality.SelectionChanged += Quality_SelectionChanged;
        _quality.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_quality, 0);
        actionRow.Children.Add(_quality);

        _favoriteButton.Margin = new Thickness(0, 0, 8, 0);
        _previewButton.Margin = new Thickness(0, 0, 8, 0);
        _resetButton.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_favoriteButton, 1);
        Grid.SetColumn(_previewButton, 2);
        Grid.SetColumn(_resetButton, 3);
        Grid.SetColumn(_applyButton, 4);
        actionRow.Children.Add(_favoriteButton);
        actionRow.Children.Add(_previewButton);
        actionRow.Children.Add(_resetButton);
        actionRow.Children.Add(_applyButton);
        Grid.SetRow(actionRow, 2);
        content.Children.Add(actionRow);

        Grid.SetColumn(content, 1);
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        _status.Text = "Catalog discovery has not started.";
        _status.FontSize = 10;
        _status.Foreground = Brush("#A8B1B6");
        _status.Margin = new Thickness(2, 14, 0, 0);
        Grid.SetRow(_status, 2);
        Grid.SetColumnSpan(_status, 2);
        root.Children.Add(_status);

        Content = root;
        _loading = false;
    }

    private async Task LoadAssetsAsync()
    {
        try
        {
            var entries = await _catalog.DiscoverAsync();
            _allAssets = entries;
            RebuildAssetList();
            _status.Text = entries.Count == 0
                ? $"No media files were found in {_catalog.RootPath}. Add approved files to the library folders."
                : $"{entries.Count} file(s) discovered from disk. SHA-256 and media headers were read from those files.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Asset discovery failed safely: {exception.Message}";
        }
    }

    private IReadOnlyList<AssetCatalogEntry> _allAssets = [];

    private void RebuildAssetList()
    {
        if (_loading) return;
        var category = _category.SelectedItem as string ?? "All assets";
        var query = _search.Text.Trim();
        var favorites = _preferences.Current.AssetLibrary.FavoriteAssetIds;
        var filtered = _allAssets.Where(asset =>
        {
            var matchesCategory = category switch
            {
                "All assets" => true,
                "Favorites" => favorites.Contains(asset.Id, StringComparer.OrdinalIgnoreCase),
                _ => string.Equals(asset.Category, category, StringComparison.OrdinalIgnoreCase)
            };
            if (!matchesCategory) return false;
            if (query.Length == 0) return true;
            var searchable = string.Join(' ', asset.Name, asset.Category, asset.Source, asset.Creator, string.Join(' ', asset.Tags));
            return searchable.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToArray();

        _assetList.Items.Clear();
        foreach (var asset in filtered) _assetList.Items.Add(CreateAssetListItem(asset));

        if (_selectedAsset is not null && filtered.All(x => !string.Equals(x.Id, _selectedAsset.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedAsset = null;
            _previewImage.Source = null;
            _details.Text = "No asset is selected.";
            UpdateActionLabels();
        }
        _status.Text = $"{filtered.Length} matching file(s) · {_allAssets.Count} discovered";
    }

    private ListBoxItem CreateAssetListItem(AssetCatalogEntry asset)
    {
        var layout = new Grid { Margin = new Thickness(2, 4, 2, 4) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(new Image
        {
            Source = TryLoadBitmap(GetThumbnailPath(asset), 180),
            Width = 72,
            Height = 52,
            Stretch = Stretch.UniformToFill,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = asset.Name,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Foreground,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{asset.Category} · {asset.Width} × {asset.Height}",
            FontSize = 9,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(text, 1);
        layout.Children.Add(text);
        return new ListBoxItem
        {
            Content = layout,
            Tag = asset,
            Padding = new Thickness(5),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }

    private void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_assetList.SelectedItem is not ListBoxItem { Tag: AssetCatalogEntry asset }) return;
        _selectedAsset = asset;
        StopPreview();
        ShowAssetDetails(asset);
        ShowStillPreview(asset);
        UpdateActionLabels();
    }

    private void ShowAssetDetails(AssetCatalogEntry asset)
    {
        var size = asset.FileSizeBytes switch
        {
            >= 1_000_000 => $"{asset.FileSizeBytes / 1_000_000d:0.00} MB",
            >= 1_000 => $"{asset.FileSizeBytes / 1_000d:0.0} KB",
            _ => $"{asset.FileSizeBytes} bytes"
        };
        var dimensions = asset.Width > 0 && asset.Height > 0
            ? $"{asset.Width} × {asset.Height}"
            : "Not available";
        var animation = asset.FrameCount > 1
            ? $"{asset.FrameCount} frames · {asset.DurationSeconds:0.##} s"
            : asset.FrameCount == 1 ? "1 frame" : "Not available";
        _details.Text =
            $"{asset.Name}  ·  {asset.Category}\n" +
            $"{dimensions}  ·  {asset.Extension.TrimStart('.').ToUpperInvariant()}  ·  {size}  ·  {animation}\n" +
            $"Source: {asset.Source}\nCreator: {asset.Creator}\nLicense: {asset.License}\n" +
            $"SHA-256: {(asset.Sha256.Length > 0 ? asset.Sha256 : "Unavailable")}" +
            (asset.ValidationWarning is null ? string.Empty : $"\nValidation: {asset.ValidationWarning}");
    }

    private void ShowStillPreview(AssetCatalogEntry asset)
    {
        try
        {
            var path = GetSafeAssetPath(asset.RelativePath);
            if (asset.Extension == ".gif")
            {
                var decoder = new GifBitmapDecoder(
                    new Uri(path),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                _gifFrames = decoder.Frames;
                _gifFrameIndex = 0;
                _previewImage.Source = _gifFrames.Count > 0 ? _gifFrames[0] : null;
                _previewImage.Visibility = Visibility.Visible;
                return;
            }

            if (asset.Extension is ".mp4" or ".webm")
            {
                _previewImage.Source = null;
                _status.Text = "Select Preview to play this media file.";
                return;
            }

            _previewImage.Source = TryLoadBitmap(path, 1800);
            _previewImage.Visibility = Visibility.Visible;
            if (_previewImage.Source is null)
            {
                _status.Text = "The selected image could not be decoded. The asset remains in the catalog for inspection.";
            }
        }
        catch (Exception exception)
        {
            _previewImage.Source = null;
            _status.Text = $"Preview decode failed: {exception.Message}";
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAsset is null)
        {
            _status.Text = "Select an asset first.";
            return;
        }

        _previewRequested = !_previewRequested;
        if (_previewRequested)
        {
            StartPreview();
        }
        else
        {
            PausePreview();
        }
        UpdateActionLabels();
    }

    private void StartPreview()
    {
        if (!_previewRequested || !IsVisible || _preferences.Current.ReduceMotion || _selectedAsset is null)
        {
            if (_preferences.Current.ReduceMotion)
            {
                _status.Text = "Reduced Motion is enabled. The first frame is shown without playback.";
            }
            return;
        }

        try
        {
            var path = GetSafeAssetPath(_selectedAsset.RelativePath);
            if (_selectedAsset.Extension == ".gif")
            {
                if (_gifFrames.Count == 0)
                {
                    ShowStillPreview(_selectedAsset);
                    return;
                }

                var frameDuration = _selectedAsset.DurationSeconds is > 0 && _selectedAsset.FrameCount > 0
                    ? Math.Clamp(_selectedAsset.DurationSeconds.Value * 1000d / _selectedAsset.FrameCount, 30d, 1000d)
                    : 100d;
                _gifTimer.Interval = TimeSpan.FromMilliseconds(frameDuration);
                _gifTimer.Start();
                _status.Text = "Playing the actual GIF frames. Playback pauses when this window is hidden or inactive.";
                return;
            }

            if (_selectedAsset.Extension is ".mp4" or ".webm")
            {
                _previewImage.Visibility = Visibility.Collapsed;
                _previewVideo.Visibility = Visibility.Visible;
                _previewVideo.Source = new Uri(path);
                _previewVideo.Position = TimeSpan.Zero;
                _previewVideo.Play();
                _status.Text = "Playing the selected media file. Playback pauses when this window is hidden or inactive.";
                return;
            }

            _status.Text = "This file is a still image; its preview is already displayed.";
        }
        catch (Exception exception)
        {
            _previewRequested = false;
            _status.Text = $"Playback could not start: {exception.Message}";
        }

        UpdateActionLabels();
    }

    private void GifTimer_Tick(object? sender, EventArgs e)
    {
        if (_gifFrames.Count == 0)
        {
            _gifTimer.Stop();
            return;
        }

        _gifFrameIndex = (_gifFrameIndex + 1) % _gifFrames.Count;
        _previewImage.Source = _gifFrames[_gifFrameIndex];
    }

    private void PausePreview()
    {
        _gifTimer.Stop();
        if (_previewVideo.Visibility == Visibility.Visible)
        {
            _previewVideo.Pause();
        }
    }

    private void ResumePreview()
    {
        if (!_previewRequested || !IsVisible || !IsActive)
        {
            return;
        }

        if (_selectedAsset?.Extension == ".gif" && !_preferences.Current.ReduceMotion)
        {
            _gifTimer.Start();
        }
        else if (_selectedAsset?.Extension is ".mp4" or ".webm" && !_preferences.Current.ReduceMotion)
        {
            _previewVideo.Play();
        }
    }

    private void StopPreview()
    {
        _previewRequested = false;
        PausePreview();
        _previewVideo.Stop();
        _previewVideo.Source = null;
        _previewVideo.Visibility = Visibility.Collapsed;
        _previewImage.Visibility = Visibility.Visible;
        _gifFrames = [];
        _gifFrameIndex = 0;
        UpdateActionLabels();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAsset is null)
        {
            _status.Text = "Select an asset first.";
            return;
        }

        var current = _preferences.Current.AssetLibrary;
        var updated = _selectedAsset.Category.ToLowerInvariant() switch
        {
            "backgrounds" => current with { SelectedBackgroundId = _selectedAsset.Id },
            "animations" => current with { SelectedAnimationId = _selectedAsset.Id },
            "visualizers" => current with { SelectedVisualizerStyleId = _selectedAsset.Id },
            _ => current
        };

        if (updated == current)
        {
            _status.Text = "This category is previewable but does not change an active player setting yet.";
            return;
        }

        try
        {
            await _preferences.SaveAsync(_preferences.Current with { AssetLibrary = updated });
            _status.Text = _selectedAsset.Category.ToLowerInvariant() switch
            {
                "backgrounds" => $"Applied {_selectedAsset.Name} as the workspace background.",
                "animations" => $"Saved {_selectedAsset.Name} as the preferred animation. Cross-window animation integration remains in progress.",
                "visualizers" => $"Saved {_selectedAsset.Name} as the visualizer style. This does not claim audio-reactive samples from GSMTC.",
                _ => $"Saved the selection for {_selectedAsset.Name}."
            };
        }
        catch (Exception exception)
        {
            _status.Text = $"Preference save failed: {exception.Message}";
        }
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = _preferences.Current;
            await _preferences.SaveAsync(current with
            {
                AssetLibrary = current.AssetLibrary with
                {
                    SelectedBackgroundId = string.Empty,
                    SelectedAnimationId = string.Empty,
                    SelectedVisualizerStyleId = "spectrum"
                }
            });
            StopPreview();
            _status.Text = "Asset selections reset to the default background, animation, and visualizer style.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Asset selections could not be reset: {exception.Message}";
        }
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAsset is null)
        {
            return;
        }

        try
        {
            var current = _preferences.Current;
            var favorites = current.AssetLibrary.FavoriteAssetIds;
            var next = favorites.Contains(_selectedAsset.Id, StringComparer.OrdinalIgnoreCase)
                ? favorites.Where(id => !string.Equals(id, _selectedAsset.Id, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : favorites.Append(_selectedAsset.Id).ToArray();

            await _preferences.SaveAsync(current with
            {
                AssetLibrary = current.AssetLibrary with { FavoriteAssetIds = [.. next] }
            });

            RebuildAssetList();
            UpdateActionLabels();
        }
        catch (Exception exception)
        {
            _status.Text = $"Favorite could not be saved: {exception.Message}";
        }
    }

    private async void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _quality.SelectedIndex < 0)
        {
            return;
        }

        var profile = _quality.SelectedIndex switch
        {
            0 => "low",
            2 => "high",
            _ => "balanced"
        };

        try
        {
            var current = _preferences.Current;
            await _preferences.SaveAsync(current with
            {
                AssetLibrary = current.AssetLibrary with { QualityProfile = profile }
            });
        }
        catch (Exception exception)
        {
            _status.Text = $"Quality profile could not be saved: {exception.Message}";
        }
    }

    private void UpdateActionLabels()
    {
        if (_selectedAsset is null)
        {
            _favoriteButton.Content = "ADD TO FAVORITES";
            _previewButton.Content = "PREVIEW";
            _applyButton.IsEnabled = false;
            _favoriteButton.IsEnabled = false;
            _previewButton.IsEnabled = false;
            return;
        }

        var favorites = _preferences.Current.AssetLibrary.FavoriteAssetIds;
        _favoriteButton.Content = favorites.Contains(_selectedAsset.Id, StringComparer.OrdinalIgnoreCase)
            ? "REMOVE FAVORITE"
            : "ADD TO FAVORITES";
        _previewButton.Content = _previewRequested ? "STOP PREVIEW" : "PREVIEW";
        _applyButton.IsEnabled = true;
        _favoriteButton.IsEnabled = true;
        _previewButton.IsEnabled = true;
    }

    private string GetThumbnailPath(AssetCatalogEntry asset)
    {
        var thumbnailStem = asset.Id.Replace('/', '_');
        var rootPrefix = _catalog.RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var extension in new[] { ".webp", ".jpg", ".jpeg", ".png" })
        {
            var thumbnailPath = Path.GetFullPath(Path.Combine(
                _catalog.RootPath,
                "Thumbnails",
                thumbnailStem + extension));

            if (thumbnailPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(thumbnailPath))
            {
                return thumbnailPath;
            }
        }

        return GetSafeAssetPath(asset.RelativePath);
    }

    private string GetSafeAssetPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_catalog.RootPath, relativePath));
        var rootPrefix = _catalog.RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Asset path escapes the configured library root.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected asset file is missing.", fullPath);
        }

        return fullPath;
    }

    private static BitmapSource? TryLoadBitmap(string path, int decodeWidth)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidDataException or UriFormatException)
        {
            return null;
        }
    }

    private static TextBlock Eyebrow(string value) => new()
    {
        Text = value,
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        Foreground = Brush("#A8E6CF")
    };

    private static Button CreateButton(string label) => new()
    {
        Content = label,
        Padding = new Thickness(12, 8, 12, 8),
        MinHeight = 34,
        FontSize = 9,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush("#F4F5F2"),
        Background = Brush("#1A2229"),
        BorderBrush = Brush("#35434D"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);

}
