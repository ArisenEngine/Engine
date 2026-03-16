using ArisenEditor.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ArisenEditor.Views;

public partial class EditorViewportView : UserControl
{
    private Image _viewportImage;

    public EditorViewportView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _viewportImage = new Image
        {
            Stretch = Stretch.Fill,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        Content = _viewportImage;
    }
}
