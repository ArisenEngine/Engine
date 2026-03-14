using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.Views;

public partial class HierarchyView : UserControl
{
    public HierarchyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
