using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArisenLauncher.Views;

public partial class PackageManagerWindow : Window
{
    public PackageManagerWindow()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        // View model command handles save, we just close or show a message
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
