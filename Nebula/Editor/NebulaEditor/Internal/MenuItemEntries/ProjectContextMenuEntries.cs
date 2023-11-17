using System.Diagnostics;
using Avalonia.Interactivity;
using NebulaEditor.Attributes;
using NebulaEditor.Utilities;
using NebulaEditor.ViewModels;

namespace NebulaEditor.Internal.MenuItemEntries;

internal static class ProjectContextMenuEntries
{
    [MenuItem("Project/Show in Explorer")]
    public static void ShowInExplorer(object? sender, RoutedEventArgs e)
    {
        if (sender != null)
        {
            var dataContext = ((Avalonia.Controls.MenuItem) sender).DataContext;
            if (dataContext != null)
            {
                if (dataContext is FileTreeNode fileTreeNode)
                {
                    Process.Start("explorer.exe", $"/select,\"{fileTreeNode.Path}\"");
                }
                else if (dataContext is ProjectHierarchyViewModel projectHierarchyViewModel)
                {
                    
                }
            }
            else
            {
                var _ = MessageBoxUtility.ShowMessageBoxStandard("Error", "Data context is null");
            }
        }
    }
}