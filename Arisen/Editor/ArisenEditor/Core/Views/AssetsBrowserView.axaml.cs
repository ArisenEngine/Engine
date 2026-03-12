using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.Views;

public partial class AssetsBrowserView : UserControl
{
    public AssetsBrowserView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
