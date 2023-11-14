using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData;
using MenuItem = NebulaEditor.Attributes.MenuItem;

namespace NebulaEditor.Utilities;

internal static class ControlsFactory
{
    internal static string CustomMenuItem = Guid.NewGuid().ToString();
    internal static string[] InternalHeaderMenus = new string[]
    {
        "File",
        "Edit",
        "Assets",
        "Entity",
        "Component",
        CustomMenuItem,
        "Window",
        "Help"
    };

    internal static string[] InternalProjectMenus = new string[]
    {
        "Create",
        "Show in Explorer",
        "Open",
        "Delete",
        "Copy Path",
        CustomMenuItem,
    };
    
    internal static string[] InternalHierarchyMenus = new string[]
    {
        "Cut",
        "Create Empty(with Transform)",
        "3D Object",
        "Light",
        "Camera",
        "UI",
        CustomMenuItem,
    };
    
    internal enum MenuType
    {
        Project,
        Header,
        Hierarchy
    }

    internal class MenuItemNode
    {
        public MethodInfo MethodInfo;
        public List<string> children;
        public string Header;
        public int level;
        public bool seperator;
    }

    private static void ParseItems(string[] internalMenus, MenuType menuType, out Dictionary<string, MenuItemNode> itemNodes)
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
                    
                    
                    MenuItemNode parent = null;
                    string parentKey = "";
                    for (int i = 1; i < menuHierarchies.Length; ++i)
                    {
                        var childKey = menuHierarchies[i];
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
                                Header = childKey,
                                children = new List<string>(),
                                MethodInfo = methodInfo,
                                level = i,
                                seperator = attribute.seperator
                            };
                            itemNodes.Add(key, parent);
                        }
                    }
                }
            }
        }
    }

    private static void SetContextMenuChildren(string[] internalMenus, ContextMenu contextMenu, Dictionary<string, MenuItemNode> itemNodes, List<MenuItemNode> userDefinedItems)
    {
        for (int i = 0; i < internalMenus.Length; ++i)
        {
            var header = internalMenus[i];
            if (itemNodes.TryGetValue(header, out var node))
            {
                // internal menus 
                
                var menuItem = new Avalonia.Controls.MenuItem()
                {
                    Header = node.Header
                };
                
                contextMenu.Items.Add(menuItem);

                if (node.children.Count > 0)
                {
                    CreateMenuItem(itemNodes, menuItem, node.children);
                }
                else
                {
                    SetMenuItemCallback(node, menuItem);
                }
                
            }
            else if (header == CustomMenuItem)
            {
                // user defined menus 
                for (int j = 0; j < userDefinedItems.Count; ++j)
                {
                    var userNode = userDefinedItems[j];
                    var userMenuItem = new Avalonia.Controls.MenuItem()
                    {
                        Header = userNode.Header
                    };
                
                    contextMenu.Items.Add(userMenuItem);
                    if (userNode.seperator && j < userDefinedItems.Count - 1)
                    {
                        contextMenu.Items.Add(new Avalonia.Controls.Separator());
                    }

                    if (userNode.children.Count > 0)
                    {
                        CreateMenuItem(itemNodes, userMenuItem, userNode.children);
                    }
                    else
                    {
                        SetMenuItemCallback(userNode, userMenuItem);
                    }
                }
            }
           
        }
    }
    
    internal static ContextMenu CreateContextMenu(MenuType menuType)
    {
        var menu = new ContextMenu();
       

        if (menuType == MenuType.Hierarchy)
        {
            ParseItems(InternalHierarchyMenus, menuType, out var itemNodes);
            ParseUserMenuItems(InternalHierarchyMenus, itemNodes, out var userDefinedItems);
            SetContextMenuChildren(InternalHierarchyMenus, menu, itemNodes, userDefinedItems);
        }
        else
        {
            ParseItems(InternalProjectMenus, menuType, out var itemNodes);
            ParseUserMenuItems(InternalProjectMenus, itemNodes, out var userDefinedItems);
            SetContextMenuChildren(InternalProjectMenus, menu, itemNodes, userDefinedItems);
        }
        return menu;
    }

    private static void ParseUserMenuItems(string[] internalMenus, Dictionary<string, MenuItemNode> itemNodes, out List<MenuItemNode> userDefinedItems)
    {
        var values = itemNodes.Values.ToArray();
        userDefinedItems = new List<MenuItemNode>();
        foreach (var menuItemNode in values)
        {
            if (menuItemNode.level == 1 && internalMenus.IndexOf(menuItemNode.Header) < 0)
            {
                userDefinedItems.Add(menuItemNode);
            }
        }
        
        userDefinedItems.Sort((a, b) =>
        {
            return string.Compare(a.Header, b.Header);
            
        });
    }
    internal static Menu CreateMenu(MenuType menuType)
    {
        var menu = new Menu();
        ParseItems(InternalHeaderMenus, menuType, out var itemNodes);

        ParseUserMenuItems(InternalHeaderMenus, itemNodes, out var userDefinedItems);
        
        for (int i = 0; i < InternalHeaderMenus.Length; ++i)
        {
            var header = InternalHeaderMenus[i];
            if (itemNodes.TryGetValue(header, out var node))
            {
                // internal menus
              
                var menuItem = new Avalonia.Controls.MenuItem()
                {
                    Header = node.Header
                };
                
                menu.Items.Add(menuItem);
                if (node.seperator && i < InternalHeaderMenus.Length - 1 && itemNodes.ContainsKey(InternalHeaderMenus[i + 1]))
                {
                    menu.Items.Add(new Avalonia.Controls.Separator());
                }

                if (node.children.Count > 0)
                {
                    CreateMenuItem(itemNodes, menuItem, node.children);
                }
                else
                {
                    SetMenuItemCallback(node, menuItem);
                }
                
            }
            else if (header == CustomMenuItem)
            {
                // user defined menus 
                for (int j = 0; j < userDefinedItems.Count; ++j)
                {
                    var userNode = userDefinedItems[j];
                    var userMenuItem = new Avalonia.Controls.MenuItem()
                    {
                        Header = userNode.Header
                    };
                
                    menu.Items.Add(userMenuItem);
                    if (userNode.seperator && j < userDefinedItems.Count - 1)
                    {
                        menu.Items.Add(new Avalonia.Controls.Separator());
                    }

                    if (userNode.children.Count > 0)
                    {
                        CreateMenuItem(itemNodes, userMenuItem, userNode.children);
                    }
                    else
                    {
                        SetMenuItemCallback(userNode, userMenuItem);
                    }
                }
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
                if (childNode.seperator && i < children.Count - 1 && itemNodes.ContainsKey(children[i + 1]))
                {
                    parentItem.Items.Add(new Avalonia.Controls.Separator());
                }

                if (!isLeafNode)
                {
                    CreateMenuItem(itemNodes, childItem, childNode.children);
                }
                else
                {
                    // leaf node. binding a callback function
                    SetMenuItemCallback(childNode, childItem);
                }
            }
        }
    }

    private static void SetMenuItemCallback(MenuItemNode childNode, Avalonia.Controls.MenuItem childItem)
    {
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