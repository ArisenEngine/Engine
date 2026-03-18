using ArisenEngine.Core.ECS;
using ArisenEditor.Core.Services;
using ArisenEditorFramework.Commands;

namespace ArisenEditor.Core.Commands;

/// <summary>
/// Command to create a new entity with a NameComponent (and optionally a TransformComponent).
/// Undoable: destroying the created entity on undo.
/// </summary>
public class CreateEntityCommand : IEditorCommand
{
    private readonly string m_EntityName;
    private readonly bool m_AddTransform;
    private readonly Entity m_Parent;
    private Entity m_CreatedEntity;

    public string Description => $"Create Entity '{m_EntityName}'";

    public CreateEntityCommand(string entityName, bool addTransform = true, Entity? parent = null)
    {
        m_EntityName = entityName;
        m_AddTransform = addTransform;
        m_Parent = parent ?? Entity.Null;
        m_CreatedEntity = Entity.Null;
    }

    public void Execute()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null) return;

        m_CreatedEntity = scene.CreateEntity();
        scene.Registry.AddComponent(m_CreatedEntity, new NameComponent { Name = m_EntityName });

        if (m_AddTransform)
        {
            scene.Registry.AddComponent(m_CreatedEntity, TransformComponent.Identity);
        }

        if (m_Parent != Entity.Null)
        {
            scene.Registry.AddComponent(m_CreatedEntity, new ParentComponent { Parent = m_Parent });
        }

        SceneManagerService.Instance.NotifyHierarchyChanged();
    }

    public void Undo()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null || m_CreatedEntity == Entity.Null) return;

        scene.DestroyEntity(m_CreatedEntity);
        SceneManagerService.Instance.NotifyHierarchyChanged();
    }
}
