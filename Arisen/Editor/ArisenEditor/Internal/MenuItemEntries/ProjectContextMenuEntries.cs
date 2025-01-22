using System.Diagnostics;
using ArisenEditor.Attributes;
using ArisenEditor.Utilities;
using ArisenEditor.ViewModels;
using Avalonia.Interactivity;

namespace ArisenEditor.Internal.MenuItemEntries;

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