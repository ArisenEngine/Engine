using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using MenuItem = NebulaEditor.Attributes.MenuItem;

namespace NebulaEditor.Utilities;

internal static class ContextMenuFactory
{
    internal enum MenuType
    {
        Assets,
        Header,
        Hierarchy
    }

    internal static ContextMenu CreateContextMenu(MenuType menuType)
    {
        var menu = new ContextMenu();
        Assembly assembly = Assembly.GetExecutingAssembly();
        Type[] types = assembly.GetTypes();
        foreach (var type in types)
        {
            MethodInfo[] methodInfos = type.GetMethods();
            foreach (var methodInfo in methodInfos)
            {
                Debug.WriteLine($"Method Name : {methodInfo.Name}");
                MenuItem attribute = (MenuItem) Attribute.GetCustomAttribute(methodInfo, typeof(MenuItem));
                if (attribute != null)
                {
                    Debug.WriteLine(methodInfo.Name + ":" + attribute.menuItem);
                }
            }
        }
        return menu;
    }

    internal static Menu CreateMenu(MenuType menuType)
    {
        var menu = new Menu();

        return menu;
    }
    
}