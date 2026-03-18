using System;
using System.Collections.Generic;
using ArisenEngine.Core.ECS;
using ArisenEditor.Core.Services;
using ArisenEditorFramework.Commands;

namespace ArisenEditor.Core.Commands;

/// <summary>
/// Command to delete an entity. Saves all component data for undo restoration.
/// </summary>
public class DeleteEntityCommand : IEditorCommand
{
    private readonly Entity m_Entity;
    private readonly string m_EntityName;

    // Saved component state for undo — list of (Type, boxed component data)
    private List<(Type Type, object Data)>? m_SavedComponents;

    public string Description => $"Delete Entity '{m_EntityName}'";

    public DeleteEntityCommand(Entity entity, string entityName)
    {
        m_Entity = entity;
        m_EntityName = entityName;
    }

    public void Execute()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null) return;

        // Save all component data before deletion
        m_SavedComponents = new List<(Type, object)>();
        foreach (var (type, pool) in scene.Registry.GetEntityComponents(m_Entity))
        {
            m_SavedComponents.Add((type, pool.GetBoxed(m_Entity)));
        }

        scene.DestroyEntity(m_Entity);
        SceneManagerService.Instance.NotifyEntityDeleted(m_Entity);
    }

    public void Undo()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null || m_SavedComponents == null) return;

        // Re-create an entity and restore all saved components.
        // Note: the restored entity will get a new ID. For a perfect undo,
        // EntityManager would need to support creating with a specific ID.
        var restoredEntity = scene.CreateEntity();

        var allPools = scene.Registry.GetAllPools();
        foreach (var (type, data) in m_SavedComponents)
        {
            if (allPools.TryGetValue(type, out var pool))
            {
                pool.SetBoxed(restoredEntity, data);
            }
        }

        SceneManagerService.Instance.NotifyEntityCreated(restoredEntity);
    }
}
