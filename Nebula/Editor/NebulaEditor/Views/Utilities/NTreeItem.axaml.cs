using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NebulaEditor.Views.Utilities;

public partial class NTreeItem : UserControl
{
    public NTreeItem()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}