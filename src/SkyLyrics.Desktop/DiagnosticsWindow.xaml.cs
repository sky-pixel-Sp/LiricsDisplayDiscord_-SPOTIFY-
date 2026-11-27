using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SkyLyrics.Desktop.ViewModels;

namespace SkyLyrics.Desktop;

public partial class DiagnosticsWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public DiagnosticsWindow(ShellViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += OnTimerTick;
        Loaded += (_, _) =>
        {
            RefreshDiagnostics();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e) => RefreshDiagnostics();

    private void RefreshDiagnostics()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var scene = _viewModel.CurrentScene;
        WorkingSetText.Text = $"{process.WorkingSet64 / 1024d / 1024d:0.0} MB";
        ManagedHeapText.Text = $"{GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d:0.0} MB";
        SceneRevisionText.Text = scene.Revision.ToString("N0");
        ArtworkBytesText.Text = scene.Track is { HasArtwork: true } track
            ? $"{track.ArtworkBytes.Length / 1024d:0.0} KB"
            : "Not supplied";
        PlaybackStatusText.Text = scene.PlaybackStatus.ToString().ToUpperInvariant();
        SceneUpdatedText.Text = scene.UpdatedAt == DateTimeOffset.UnixEpoch
            ? "No scene yet"
            : scene.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
