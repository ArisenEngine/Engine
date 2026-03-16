using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Controls.Models.TreeDataGrid;
using ArisenEditor.ViewModels;
using ArisenEditor.Core.Services;

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

    private void OnFolderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TreeDataGrid grid && grid.RowSelection?.SelectedItems != null)
        {
            foreach (var item in grid.RowSelection.SelectedItems)
            {
                if (item is FileTreeNode node && node.IsBranch)
                {
                    node.IsExpanded = !node.IsExpanded;
                }
            }
        }
        e.Handled = true;
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TreeDataGrid grid && grid.RowSelection?.SelectedItems != null)
        {
            foreach (var item in grid.RowSelection.SelectedItems)
            {
                if (item is FileTreeNode node)
                {
                    if (node.IsBranch)
                    {
                        // In flat view, maybe we want to navigate inside?
                        // For now, let the ViewModel handle folder navigation if needed,
                        // or just expand if possible.
                    }
                    else if (node.Name.EndsWith(".arisen"))
                    {
                        // User double clicked a scene. Load it.
                        SceneManagerService.Instance.LoadScene(node.Path);
                    }
                }
            }
        }
        e.Handled = true;
    }
}
