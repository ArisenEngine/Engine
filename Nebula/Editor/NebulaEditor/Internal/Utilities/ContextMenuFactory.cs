using System.Collections.Generic;
using Avalonia.Controls;

namespace NebulaEditor.Utilities;

internal static class ContextMenuFactory
{
    internal enum MenuType
    {
        Folder,
        WorldHierarchy
    }

    internal static ContextMenu CreateContextMenu(MenuType menuType)
    {
        switch (menuType)
        {
            case MenuType.Folder:
                return CreateFolderContextMenu();
        }

        return new ContextMenu();
    }

    private static ContextMenu CreateFolderContextMenu()
    {
        var menu = new ContextMenu();
        
        var renameMenu = new MenuItem()
        {
           Header = "Rename"
        };
        renameMenu.Click += (sender, args) =>
        {
            
        };
        menu.Items.Add(renameMenu);
        return menu;
    }
}