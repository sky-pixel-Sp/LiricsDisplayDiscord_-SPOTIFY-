namespace SkyLyrics.Desktop;

internal static class Program
{
    [System.STAThread]
    public static void Main()
    {
        if (!System.Windows.Forms.Application.SetHighDpiMode(
                System.Windows.Forms.HighDpiMode.PerMonitorV2))
        {
            throw new System.InvalidOperationException(
                "SkyLyrics could not initialize Per-Monitor V2 DPI awareness before creating its first window.");
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
