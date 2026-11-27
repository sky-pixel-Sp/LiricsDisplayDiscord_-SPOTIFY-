using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using SkyLyrics.Desktop.ViewModels;

namespace SkyLyrics.Desktop;

public partial class FloatingPlayerWindow : Window
{
    public FloatingPlayerWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is WpfButton)
            {
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
