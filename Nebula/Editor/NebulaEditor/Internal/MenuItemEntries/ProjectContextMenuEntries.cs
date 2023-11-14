using System.Diagnostics;
using Avalonia.Interactivity;
using NebulaEditor.Attributes;
using NebulaEditor.ViewModels;

namespace NebulaEditor.Internal.MenuItemEntries;

internal static class ProjectContextMenuEntries
{
    [MenuItem("Project/Show in Explorer")]
    public static void ShowInExplorer(object? sender, RoutedEventArgs e)
    {
        if (sender != null)
        {
            FolderTreeNode treeNode = ((Avalonia.Controls.MenuItem) sender).DataContext as FolderTreeNode;
            Process.Start("explorer.exe", $"/select,\"{treeNode.Path}\"");
        }
    }
}