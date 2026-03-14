using System.Collections.Generic;
using ArisenEditorFramework.UI.Menus;
using ArisenEditor.ViewModels;
using ReactiveUI;

namespace ArisenEditor.Core.Services;

public class AssetBrowserMenuProvider : IMenuProvider
{
    public IEnumerable<MenuAction> GetMenuItems(string menuId, object? context)
    {
        if (menuId == "Assets.CreateMenu" || (menuId == "Assets.ContextMenu" && context == null))
        {
            yield return new MenuAction("Folder", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine("Creating Folder...");
            }));
            
            yield return new MenuAction("Material", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine("Creating Material...");
            }));

            yield return new MenuAction("Shader", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine("Creating Shader...");
            }));
            
            var scriptMenu = new MenuAction("Script");
            scriptMenu.Children.Add(new MenuAction("C# Script", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine("Creating C# Script...");
            })));
            scriptMenu.Children.Add(new MenuAction("C++ Script", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine("Creating C++ Script...");
            })));
            yield return scriptMenu;
        }
        else if (menuId == "Assets.ContextMenu" && context is FileTreeNode node)
        {
            yield return new MenuAction("Open", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Opening {node.Name}...");
            }));

            yield return new MenuAction("Copy Path", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Copying path of {node.Name}...");
            }));

            yield return new MenuAction("Delete", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Deleting {node.Name}...");
            }));
            
            yield return new MenuAction("Show in Explorer", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Showing {node.Name} in explorer...");
            }));
        }
    }
}
