using System.Collections.Generic;
using ArisenEditorFramework.UI.Menus;
using ArisenEditor.ViewModels;
using ReactiveUI;

namespace ArisenEditor.Core.Services;

public class HierarchyMenuProvider : IMenuProvider
{
    public IEnumerable<MenuAction> GetMenuItems(string menuId, object? context)
    {
        if (menuId == "Hierarchy.CreateMenu" || (menuId == "Hierarchy.ContextMenu" && (context == null || context is SceneNodeViewModel)))
        {
            yield return new MenuAction("Empty Entity", ReactiveCommand.Create(() => 
            {
                // Logic to create empty entity
                System.Diagnostics.Debug.WriteLine("Creating Empty Entity...");
            }));
            
            yield return new MenuAction("Camera", ReactiveCommand.Create(() => 
            {
                // Logic to create camera
                System.Diagnostics.Debug.WriteLine("Creating Camera...");
            }));

            yield return new MenuAction("Light", ReactiveCommand.Create(() => 
            {
                // Logic to create light
                System.Diagnostics.Debug.WriteLine("Creating Light...");
            }));
        }
        else if (menuId == "Hierarchy.ContextMenu" && context is EntityNodeViewModel node)
        {
            yield return new MenuAction("Rename", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Renaming {node.Name}...");
            }));

            yield return new MenuAction("Delete", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Deleting {node.Name}...");
            }));
            
            yield return new MenuAction("Clone", ReactiveCommand.Create(() => 
            {
                System.Diagnostics.Debug.WriteLine($"Cloning {node.Name}...");
            }));
        }
    }
}
