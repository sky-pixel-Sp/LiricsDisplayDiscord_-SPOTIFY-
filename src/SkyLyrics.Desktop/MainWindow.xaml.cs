using System.Reflection;
using System.Windows;

namespace SkyLyrics.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}

public sealed class ShellViewModel
{
    private static readonly Version AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);

    public string StatusTitle => "The foundation is in place.";

    public string StatusDescription =>
        "SkyLyrics now has a Windows-native .NET 10 application shell, a platform-neutral scene contract, " +
        "and an isolated output-routing boundary. Playback and presentation features will be added in later phases.";

    public string NextMilestone => "Phase 1 — GSMTC adapter and monotonic PlaybackClock";

    public string VersionLabel => $"VERSION {AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";
}
