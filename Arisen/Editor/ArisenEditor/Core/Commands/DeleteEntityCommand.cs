using System;
using System.Collections.Generic;
using ArisenEngine.Core.ECS;
using ArisenEditor.Core.Services;
using ArisenKernel.Contracts;

namespace ArisenEditor.Core.Commands;

/// <summary>
/// Command to delete an entity. Saves all component data for undo restoration.
/// </summary>
public class DeleteEntityCommand : ICommand
{
    private readonly Entity m_Entity;
    private readonly string m_EntityName;
    private Entity m_OldParent;

    // Saved component state for undo 鈥?list of (Type, boxed component data)
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
            // Do not save Intrusive Linked List structs, we manage those via ReparentEntity natively!
            if (type == typeof(ParentComponent) || type == typeof(SiblingComponent) || type == typeof(ChildComponent))
                continue;

            m_SavedComponents.Add((type, pool.GetBoxed(m_Entity)));
        }

        m_OldParent = scene.Registry.HasComponent<ParentComponent>(m_Entity) ? scene.Registry.GetComponent<ParentComponent>(m_Entity).Parent : Entity.Null;
        
        // Detach structurally from ECS tree before deleting
        MoveEntityCommand.ReparentEntity(scene.Registry, m_Entity, Entity.Null);

        scene.DestroyEntity(m_Entity);
        SceneManagerService.Instance.NotifyEntityDeleted(m_Entity);
    }

    public void Undo()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null || m_SavedComponents == null) return;

        // Re-create the entity mapping the EXACT original ID. This solves deep dependencies.
        var restoredEntity = scene.Registry.CreateEntity(m_Entity.Id);

        var allPools = scene.Registry.GetAllPools();
        foreach (var (type, data) in m_SavedComponents)
        {
            if (allPools.TryGetValue(type, out var pool))
            {
                pool.SetBoxed(restoredEntity, data);
            }
        }

        // Restore structural hierarchy manually
        MoveEntityCommand.ReparentEntity(scene.Registry, restoredEntity, m_OldParent);

        SceneManagerService.Instance.NotifyEntityCreated(restoredEntity);
    }
}

