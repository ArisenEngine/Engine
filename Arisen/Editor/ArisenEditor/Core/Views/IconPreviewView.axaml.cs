using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.Views;

public partial class IconPreviewView : UserControl
{
    public IconPreviewView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
