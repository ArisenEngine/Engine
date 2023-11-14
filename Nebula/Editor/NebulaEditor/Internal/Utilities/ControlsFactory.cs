using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData;
using MenuItem = NebulaEditor.Attributes.MenuItem;

namespace NebulaEditor.Utilities;

internal static class ControlFactory
{
    internal static string CustomHeaderMenu = Guid.NewGuid().ToString();
    internal static string[] InternalHeaderMenus = new string[]
    {
        "File",
        "Edit",
        "Assets",
        "Entity",
        "Component",
        CustomHeaderMenu,
        "Window",
        "Help"
    };
    
    internal enum MenuType
    {
        Assets,
        Header,
        Hierarchy
    }

    internal class MenuItemNode
    {
        public MethodInfo MethodInfo;
        public List<string> children;
        public string Header;
        public string key;
    }

    private static void ParseItems(MenuType menuType, out Dictionary<string, MenuItemNode> itemNodes)
    {
        itemNodes = new Dictionary<string, MenuItemNode>();
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        Type[] types = assembly.GetTypes();
        
        foreach (var type in types)
        {
            MethodInfo[] methodInfos = type.GetMethods();
            foreach (var methodInfo in methodInfos)
            {
                MenuItem attribute = (MenuItem) Attribute.GetCustomAttribute(methodInfo, typeof(MenuItem));
                if (attribute != null)
                {
                    var menuHierarchies = attribute.menuItem.Split(MenuItem.kMenuItemSeparators);
                    
                    if (menuHierarchies.Length <= 1)
                    {
                        // NOTE: menu root should not be as a leaf node
                        // TODO: add a warning message to console
                        continue;
                    }
                    
                    if (menuHierarchies[0] != menuType.ToString())
                    {
                        // Undefined menu type
                        continue;
                    }

                    bool isUserDefinedMenu = InternalHeaderMenus.IndexOf(menuHierarchies[1]) < 0;
                    
                    MenuItemNode parent = null;
                    string parentKey = "";
                    for (int i = 1; i < menuHierarchies.Length; ++i)
                    {
                        var childHeader = menuHierarchies[i];
                        var childKey = i == 1 && isUserDefinedMenu ? CustomHeaderMenu : childHeader;
                        var key =  string.IsNullOrEmpty(parentKey) ? childKey : parentKey + MenuItem.kMenuItemSeparators + childKey;
                        parentKey = key;
                        if (itemNodes.TryGetValue(key, out var node))
                        {
                            parent = node;
                        }
                        else
                        {
                            if (parent != null)
                            {
                                parent.children.Add(key);
                            }

                            parent = new MenuItemNode()
                            {
                                Header = childHeader,
                                children = new List<string>(),
                                MethodInfo = methodInfo,
                                key = key
                            };
                            itemNodes.Add(key, parent);
                        }
                    }
                }
            }
        }
    }
    
    internal static ContextMenu CreateContextMenu(MenuType menuType)
    {
        var menu = new ContextMenu();
        
        return menu;
    }

    internal static Menu CreateMenu(MenuType menuType)
    {
        var menu = new Menu();
        ParseItems(menuType, out var itemNodes);
        
        for (int i = 0; i < InternalHeaderMenus.Length; ++i)
        {
            var header = InternalHeaderMenus[i];
            if (itemNodes.TryGetValue(header, out var node))
            {
                // NOTE: root menu are consider to be always has children node
                Debug.Assert(node.children.Count > 0);
                var parentNode = new Avalonia.Controls.MenuItem()
                {
                    Header = node.Header
                };
                
                menu.Items.Add(parentNode);

                CreateMenuItem(itemNodes, parentNode, node.children);
                
            }
           
        }
        return menu;
    }

    private static void CreateMenuItem(Dictionary<string, MenuItemNode> itemNodes, Avalonia.Controls.MenuItem parentItem, List<string> children)
    {
        for (int i = 0; i < children.Count; ++i)
        {
            if (itemNodes.TryGetValue(children[i], out var childNode))
            {
                bool isLeafNode = childNode.children.Count <= 0;
                var childItem = new Avalonia.Controls.MenuItem()
                {
                    Header = childNode.Header
                };
                
                parentItem.Items.Add(childItem);

                if (!isLeafNode)
                {
                    CreateMenuItem(itemNodes, childItem, childNode.children);
                }
                else
                {
                    // leaf node. binding a callback function
                    var methodInfo = childNode.MethodInfo;
                    childItem.Click += (object? sender, RoutedEventArgs? e) =>
                    {
                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length <= 0)
                        {
                            methodInfo?.Invoke(null, new object?[]
                            {
                                
                            });
                        }
                        else if (parameters.Length == 2 
                                 && parameters[0].ParameterType == typeof(object) 
                                 && parameters[1].ParameterType == typeof(RoutedEventArgs))
                        {
                            methodInfo.Invoke(null, new object?[]
                            {
                                sender,
                                e
                            });
                        }
                        else
                        {
                            // TODO: replace with warning message
                            var _ = MessageBoxUtility.ShowMessageBoxStandard("Error",
                                "MenuItem callback only support zero parameter or two parameters (object sender, RoutedEventArgs e)");
                        }
                    };
                }
            }
        }
    }

}