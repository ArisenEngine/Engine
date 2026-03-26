using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArisenLauncher.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
