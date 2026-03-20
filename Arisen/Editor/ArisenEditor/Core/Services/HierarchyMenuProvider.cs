using System.Collections.Generic;
using ArisenEditorFramework.UI.Menus;
using ArisenEditor.ViewModels;
using ArisenEditor.Core.Commands;
using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
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
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Empty Entity"));
            }));
            
            yield return new MenuAction("Camera", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Camera"));
            }));

            yield return new MenuAction("Light", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Light"));
            }));
        }
        else if (menuId == "Hierarchy.ContextMenu" && context is EntityNodeViewModel node)
        {
            yield return new MenuAction("Create Empty Child", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Empty Entity", true, node.Entity));
            }));

            yield return new MenuAction("Create Child Camera", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Camera", true, node.Entity));
            }));

            yield return new MenuAction("Create Child Light", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new CreateEntityCommand("Light", true, node.Entity));
            }));

            yield return new MenuAction("Rename", ReactiveCommand.Create(() => 
            {
                node.IsRenaming = true;
            }));

            yield return new MenuAction("Delete", ReactiveCommand.Create(() => 
            {
                ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(new DeleteEntityCommand(node.Entity, node.Name));
            }));
            
            yield return new MenuAction("Clone", ReactiveCommand.Create(() => 
            {
                // Cloning requires iterating through all components and duplicating. A bit complex for now.
                System.Diagnostics.Debug.WriteLine($"Cloning {node.Name}...");
            }));
        }
    }
}




