using Avalonia.Controls;
using Avalonia.Input;
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

        var treeView = this.FindControl<TreeView>("MainTreeView");
        if (treeView != null)
        {
            treeView.AddHandler(PointerPressedEvent, OnTreeViewPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
    }

    private void OnTreeViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (e.Source is Control control)
            {
                if (control.DataContext is ViewModels.EntityNodeViewModel entityNode)
                {
                    if (this.DataContext is ViewModels.HierarchyViewModel vm)
                        vm.SelectedItem = entityNode;
                }
                else if (control.DataContext is ViewModels.SceneNodeViewModel sceneNode)
                {
                    if (this.DataContext is ViewModels.HierarchyViewModel vm)
                        vm.SelectedItem = sceneNode;
                }
                else if (control.DataContext is ViewModels.HierarchyViewModel vw)
                {
                    vw.SelectedItem = null;
                }
            }
        }
    }
}
