using System.Windows;
using System.Windows.Input;
using SkyLyrics.Desktop.ViewModels;

namespace SkyLyrics.Desktop;

public partial class LyricsFocusWindow : Window
{
    public LyricsFocusWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
