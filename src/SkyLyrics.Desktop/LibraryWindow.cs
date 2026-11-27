using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SkyLyrics.Application.Lyrics;
using SkyLyrics.Desktop.ViewModels;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignmentKind = System.Windows.HorizontalAlignment;

namespace SkyLyrics.Desktop;

public sealed class LibraryWindow : Window
{
    private sealed record LibraryItem(LyricsCacheEntry Entry, string Title, string Subtitle, string SearchText);

    private readonly ShellViewModel _viewModel;
    private readonly ILyricsLibrary _library;
    private readonly ObservableCollection<LibraryItem> _items = [];
    private readonly ICollectionView _view;
    private readonly ListBox _list = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _details = new();
    private readonly TextBlock _status = new();
    private readonly Button _restore = new();
    private readonly Button _delete = new();
    private bool _loading;
    private bool _restoring;

    public LibraryWindow(ShellViewModel viewModel, ILyricsLibrary library)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _library = library ?? throw new ArgumentNullException(nameof(library));

        Title = "SkyLyrics · Library";
        Width = 1060;
        Height = 760;
        MinWidth = 780;
        MinHeight = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0D10");
        Foreground = Brush("#F4F5F2");
        FontFamily = new FontFamily("Segoe UI Variable Display");

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItem;
        BuildInterface();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void BuildInterface()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel();
        titles.Children.Add(new TextBlock
        {
            Text = "LOCAL LYRICS LIBRARY",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        titles.Children.Add(new TextBlock
        {
            Text = "Your verified cached lyrics",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 0, 0)
        });
        titles.Children.Add(new TextBlock
        {
            Text = "Only records present in your local SQLite cache appear here.",
            FontSize = 11,
            Foreground = Brush("#A8B1B6"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        heading.Children.Add(titles);
        var refresh = CreateButton("REFRESH");
        refresh.Click += async (_, _) => await RefreshAsync();
        Grid.SetColumn(refresh, 1);
        refresh.VerticalAlignment = VerticalAlignment.Center;
        heading.Children.Add(refresh);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var left = new Grid { Margin = new Thickness(0, 0, 12, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _search.Padding = new Thickness(11, 8, 11, 8);
        _search.FontSize = 12;
        _search.Background = Brush("#12161B");
        _search.Foreground = Brush("#F4F5F2");
        _search.BorderBrush = Brush("#28323A");
        _search.Margin = new Thickness(0, 0, 0, 10);
        _search.ToolTip = "Search cached track, artist, album or provider";
        _search.TextChanged += (_, _) => _view.Refresh();
        left.Children.Add(_search);

        _list.ItemsSource = _view;
        _list.SelectionChanged += (_, _) => UpdateSelection();
        _list.BorderBrush = Brush("#28323A");
        _list.Background = Brush("#12161B");
        _list.Foreground = Brush("#F4F5F2");
        _list.Padding = new Thickness(6);
        _list.ItemTemplate = CreateItemTemplate();
        Grid.SetRow(_list, 1);
        left.Children.Add(_list);
        Grid.SetRow(left, 1);
        root.Children.Add(left);

        var right = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            Background = Brush("#12161B"),
            BorderBrush = Brush("#28323A"),
            BorderThickness = new Thickness(1)
        };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "SELECTED RECORD",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#A8E6CF")
        });
        _details.Text = "Select an existing cache record.";
        _details.TextWrapping = TextWrapping.Wrap;
        _details.FontSize = 12;
        _details.Foreground = Brush("#D5DED8");
        _details.Margin = new Thickness(0, 11, 0, 0);
        panel.Children.Add(_details);

        _restore = CreateButton("RESTORE TO NOW PLAYING");
        _restore.Margin = new Thickness(0, 20, 0, 0);
        _restore.IsEnabled = false;
        _restore.Click += Restore_Click;
        panel.Children.Add(_restore);

        _delete = CreateButton("DELETE CACHE ENTRY");
        _delete.Margin = new Thickness(0, 9, 0, 0);
        _delete.IsEnabled = false;
        _delete.Background = Brush("#332225");
        _delete.BorderBrush = Brush("#694047");
        _delete.Click += Delete_Click;
        panel.Children.Add(_delete);

        var note = new TextBlock
        {
            Text = "Restore is allowed only when the selected record's title and artist match the active Windows media session. Deleting removes only the local cache entry.",
            FontSize = 10,
            Foreground = Brush("#A8B1B6"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0)
        };
        panel.Children.Add(note);
        right.Child = panel;
        Grid.SetColumn(right, 1);
        Grid.SetRow(right, 1);
        root.Children.Add(right);

        _status.Text = "Loading cached records…";
        _status.FontSize = 10;
        _status.Foreground = Brush("#A8B1B6");
        _status.Margin = new Thickness(0, 13, 0, 0);
        Grid.SetRow(_status, 2);
        Grid.SetColumnSpan(_status, 2);
        root.Children.Add(_status);
        Content = root;
    }

    private static DataTemplate CreateItemTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.MarginProperty, new Thickness(7, 6, 7, 6));

        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(LibraryItem.Title)));
        title.SetValue(TextBlock.FontSizeProperty, 13d);
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        title.SetValue(TextBlock.ForegroundProperty, Brush("#F4F5F2"));
        panel.AppendChild(title);

        var subtitle = new FrameworkElementFactory(typeof(TextBlock));
        subtitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(LibraryItem.Subtitle)));
        subtitle.SetValue(TextBlock.FontSizeProperty, 10d);
        subtitle.SetValue(TextBlock.ForegroundProperty, Brush("#A8B1B6"));
        subtitle.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));
        panel.AppendChild(subtitle);

        return new DataTemplate { VisualTree = panel };
    }

    private bool FilterItem(object value)
    {
        if (value is not LibraryItem item) return false;
        var query = _search.Text.Trim();
        return query.Length == 0 ||
               item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        _status.Text = "Reading local SQLite lyrics cache…";
        try
        {
            var entries = await _library.GetRecentAsync(200, CancellationToken.None);
            _items.Clear();
            foreach (var entry in entries)
            {
                var document = entry.Document;
                var title = string.IsNullOrWhiteSpace(document.Title) ? "Untitled cached record" : document.Title;
                var artist = string.IsNullOrWhiteSpace(document.Artist) ? "Artist unavailable" : document.Artist;
                var album = string.IsNullOrWhiteSpace(document.Album) ? string.Empty : $" · {document.Album}";
                var subtitle = $"{artist}{album}  ·  {entry.Provider}  ·  {document.SyncMode}  ·  {entry.CachedAtUtc.ToLocalTime():g}";
                var search = string.Join(" ", title, artist, document.Album, entry.Provider);
                _items.Add(new LibraryItem(entry, title, subtitle, search));
            }

            _view.Refresh();
            _status.Text = $"Loaded {_items.Count} actual cache record(s) from local SQLite.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not read local lyrics cache: {exception.Message}";
        }
        finally
        {
            _loading = false;
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        var item = _list.SelectedItem as LibraryItem;
        _restore.IsEnabled = item is not null && !_restoring;
        _delete.IsEnabled = item is not null && !_restoring;
        if (item is null)
        {
            _details.Text = "Select an existing cache record.";
            return;
        }

        var doc = item.Entry.Document;
        _details.Text =
            $"{item.Title}

" +
            $"{doc.Artist ?? "Artist unavailable"}
" +
            $"{doc.Album ?? "Album unavailable"}

" +
            $"Provider: {item.Entry.Provider}
" +
            $"Provider ID: {item.Entry.ProviderId?.ToString() ?? "Not supplied"}
" +
            $"Synchronization: {doc.SyncMode}
" +
            $"Lines: {doc.Lines.Length}
" +
            $"Cached: {item.Entry.CachedAtUtc.ToLocalTime():f}";
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_list.SelectedItem is not LibraryItem item || _restoring) return;
        _restoring = true;
        UpdateSelection();
        try
        {
            if (_viewModel.LoadCachedLyrics(item.Entry))
            {
                _status.Text = $"Restored {item.Title} from the local cache.";
                Close();
            }
            else
            {
                _status.Text = _viewModel.LyricsStatusText;
            }
        }
        finally
        {
            _restoring = false;
            UpdateSelection();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_list.SelectedItem is not LibraryItem item || _restoring) return;
        _restoring = true;
        UpdateSelection();
        try
        {
            var removed = await _library.DeleteAsync(item.Entry.CacheKey, CancellationToken.None);
            if (removed)
            {
                _items.Remove(item);
                _view.Refresh();
                _status.Text = $"Deleted local cache entry: {item.Title}.";
            }
            else
            {
                _status.Text = "The selected cache entry no longer exists.";
            }
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not delete cache entry: {exception.Message}";
        }
        finally
        {
            _restoring = false;
            UpdateSelection();
        }
    }

    private static Button CreateButton(string label) => new()
    {
        Content = label,
        Padding = new Thickness(13, 9, 13, 9),
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        Foreground = Brush("#F4F5F2"),
        Background = Brush("#1C302A"),
        BorderBrush = Brush("#A8E6CF"),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value)!);
}
